using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Sels.HiveMind.HiveLog;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker
{
    /// <summary>
    /// A swarm host that executes dequeued jobs.
    /// </summary>
    public class WorkerSwarmHost : BackgroundJobSwarmHost<WorkerSwarmHostOptions, WorkerSwarmDefaultHostOptions>
    {
        // Statics
        private static Type VoidTaskType = typeof(Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult"));

        // Fields
        private readonly object _lock = new object();

        // State
        private WorkerSwarmHostOptions _currentOptions;
        private CancellationTokenSource _reloadSource;

        // Properties
        /// <inheritdoc/>
        protected override WorkerSwarmHostOptions Options { get { lock (_lock) { return _currentOptions; } } }
        /// <summary>
        /// The current options being used by the swarm host.
        /// </summary>
        public IWorkerSwarmHostOptions CurrentOptions => Options;
        /// <inheritdoc/>
        protected override string SwarmPrefix => $"Worker.";

        /// <inheritdoc cref="WorkerSwarmHost"/>
        /// <param name="defaultWorkerOptions">The default worker options for this swarm</param>
        /// <param name="defaultOptions"><inheritdoc cref="_defaultOptions"/></param>
        /// <param name="taskManager">Used to manage dromes</param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        public WorkerSwarmHost(WorkerSwarmHostOptions options, IOptionsMonitor<WorkerSwarmDefaultHostOptions> defaultOptions, ITaskManager taskManager, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider) : base(HiveMindConstants.Queue.BackgroundJobProcessQueueType, defaultOptions, taskManager, jobQueueProvider, schedulerProvider)
        {
            SetOptions(options);
        }

        /// <summary>
        /// Overwrites the options for this instance. Host will restart.
        /// </summary>
        /// <param name="options"><inheritdoc cref="Options"/></param>
        public void SetOptions(WorkerSwarmHostOptions options)
        {
            options.ValidateArgument(nameof(options));
            options.ValidateAgainstProfile<WorkerSwarmHostOptionsValidationProfile, WorkerSwarmHostOptions, string>().ThrowOnValidationErrors();

            lock (_lock)
            {
                _currentOptions = options;
                if (_reloadSource != null) _reloadSource.Cancel();
                _reloadSource = new CancellationTokenSource();
            }
        }

        /// <inheritdoc/>
        public override async Task RunAsync(IDaemonExecutionContext context, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var swarmTokenSource = new CancellationTokenSource();
                context.Log($"Worker swarm <{HiveLog.Daemon.Name}> setting up", context.Daemon.Name);
                try
                {
                    // Monitoring for changes and restart if changes are detected
                    using var tokenSourceRegistration = token.Register(() => swarmTokenSource.Cancel());
                    using var defaultOptionsMonitorRegistration = _defaultOptions.OnChange((o, n) =>
                    {
                        if (n.EqualsNoCase(context.Daemon.Colony.Environment)) swarmTokenSource.Cancel();
                    });
                    CancellationTokenSource reloadSource;
                    lock (_lock)
                    {
                        reloadSource = _reloadSource;
                    }
                    using var reloadSourceRegistration = reloadSource.Token.Register(() => swarmTokenSource.Cancel());

                    await base.RunAsync(context, swarmTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    context.Log(LogLevel.Debug, $"Worker swarm <{HiveLog.Daemon.Name}> cancelled", context.Daemon.Name);
                    continue;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        protected override bool CanBeProcessed(IDaemonExecutionContext context, IDroneState<WorkerSwarmHostOptions> state, IDequeuedJob job, IReadOnlyBackgroundJob backgroundJob, HiveMindOptions options, out TimeSpan? delay)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));

            delay = null;

            // Execution matches
            if (!job.ExecutionId.Equals(backgroundJob.ExecutionId))
            {
                context.Log(LogLevel.Warning, $"Execution id of dequeued job does not match execution id of background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>. Can't process", backgroundJob.Id, backgroundJob.Environment);
                return false;
            }
            else if (!(backgroundJob.State is EnqueuedState))
            {
                context.Log(LogLevel.Warning, $"State of background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not <{nameof(EnqueuedState)}> but <{HiveLog.Job.LockHolder}>. Can't process", backgroundJob.Id, backgroundJob.Environment, backgroundJob.State);
                return false;
            }

            return true;
        }
        /// <inheritdoc/>
        protected override async Task ProcessJobAsync(IDaemonExecutionContext context, IDroneState<WorkerSwarmHostOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));
            options.ValidateArgument(nameof(options));

            // Activate job and middleware to execute
            var serviceProvider = context.ServiceProvider;
            var environment = context.Daemon.Colony.Environment;
            var taskManager = serviceProvider.GetRequiredService<ITaskManager>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var activator = serviceProvider.GetRequiredService<IActivator>();
            await using var activatorScope = await activator.CreateActivatorScope(serviceProvider);
            var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
            var (invocationDelegate, jobContextIndex, cancellationTokenIndex) = GetJobInvocationInfo(context, options, memoryCache, backgroundJob.Invocation);
            object instance = null;
            if (!backgroundJob.Invocation.MethodInfo.IsStatic) instance = await activatorScope.Active(backgroundJob.Invocation.Type).ConfigureAwait(false);
            var middleware = await ActiveMiddleware(activatorScope, GetMiddleware(context, options, state, backgroundJob, memoryCache)).ConfigureAwait(false);

            // Get storage
            var storageProvider = serviceProvider.GetRequiredService<IStorageProvider>();
            await using var storageScope = await storageProvider.GetStorageAsync(environment, token).ConfigureAwait(false);
            var storage = storageScope.Component;

            // Create execution context
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            await using (var executionContext = new BackgroundJobExecutionContext(context.Daemon.Colony.Name,
                                                                                 state,
                                                                                 backgroundJob,
                                                                                 instance,
                                                                                 backgroundJob.Invocation.Arguments.HasValue() ? backgroundJob.Invocation.Arguments.ToArray() : Array.Empty<object>(),
                                                                                 state.Swarm.Options.LogLevel ?? _defaultOptions.CurrentValue.LogLevel,
                                                                                 state.Swarm.Options.LogFlushInterval ?? _defaultOptions.CurrentValue.LogFlushInterval,
                                                                                 taskManager,
                                                                                 storage,
                                                                                 loggerFactory?.CreateLogger(backgroundJob.Invocation.Type)))
            {
                context.Log(LogLevel.Debug, $"Drone <{state.FullName}> setting background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> to executing", job.JobId, environment);
                // Set to executing
                var isExecuting = await backgroundJob.ChangeStateAsync(new ExecutingState(context.Daemon.Colony.Name, state.Swarm.Name, state.Name) { Reason = $"Being executed by drone <{state.FullName}>" }, token).ConfigureAwait(false);

                if (isExecuting)
                {
                    await backgroundJob.SaveChangesAsync(true, token).ConfigureAwait(false);

                    context.Log($"Drone <{HiveLog.Swarm.DroneName}> executing background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", state.FullName, job.JobId, environment);

                    // Invoke middleware and job
                    await ExecuteJobAsync(context, state, executionContext, invocationDelegate, jobContextIndex, cancellationTokenIndex, middleware, token).ConfigureAwait(false);
                    context.Log($"Drone <{HiveLog.Swarm.DroneName}> executed background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> in {stopwatch.Elapsed.PrintTotalMs()}", state.FullName, job.JobId, environment);

                    // Parse result and set final state
                    if (backgroundJob.State.Name.EqualsNoCase(ExecutingState.StateName))
                    {
                        if (executionContext.Result is Exception ex)
                        {
                            context.Log($"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> executed by Drone <{HiveLog.Swarm.DroneName}> failed with exception. Setting failed state", ex, job.JobId, environment, state.FullName);
                            await backgroundJob.ChangeStateAsync(new FailedState(ex) { Reason = $"Job execution result was exception"}, token).ConfigureAwait(false);
                        }
                        else
                        {
                            stopwatch.Stop();
                            await backgroundJob.ChangeStateAsync(new SucceededState(executionContext.Duration, stopwatch.Elapsed, DateTime.UtcNow - backgroundJob.CreatedAtUtc, executionContext.Result)
                            {
                                Reason = $"Job executed without throwing exception"
                            }, token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not longer in executing state after being executed by Drone <{HiveLog.Swarm.DroneName}>. State now is <{HiveLog.BackgroundJob.State}>", job.JobId, environment, state.FullName, backgroundJob.State.Name);
                    }
                }
                else
                {
                    context.Log(LogLevel.Warning, $"Drone <{HiveLog.Swarm.DroneName}> tried setting background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> to executing but state was transitioned into <{HiveLog.BackgroundJob.State}>", state.FullName, job.JobId, environment, backgroundJob.State.Name);
                }

                // Save changes and release job
                await backgroundJob.SaveChangesAsync(false, token).ConfigureAwait(false);
                await job.CompleteAsync(token);
            }
        }

        private IEnumerable<IMiddlewareInfo> GetMiddleware(IDaemonExecutionContext context, HiveMindOptions hiveOptions, IDroneState<WorkerSwarmHostOptions> state, IReadOnlyBackgroundJob job, IMemoryCache cache = null)
        {
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));

            if (job.Middleware.HasValue())
            {
                foreach (var middleware in job.Middleware)
                {
                    context.Log($"Drone <{state.FullName}> got middleware <{middleware.Type}> with priority <{middleware.Priority}> from job itself for background job <{HiveLog.Job.Id}>", job.Id);
                    yield return middleware;
                }
            }

            ISwarmState<WorkerSwarmHostOptions> currentSwarm = state.Swarm;
            bool useFromParentSwarm;
            do
            {
                useFromParentSwarm = currentSwarm.Options.UseMiddlewareFromParentSwarms;

                if (currentSwarm.Options.Middelware.HasValue())
                {
                    foreach (var middleware in currentSwarm.Options.Middelware)
                    {
                        context.Log($"Drone <{state.FullName}> got middleware <{middleware.TypeName}> with priority <{middleware.Priority}> from swarm <{currentSwarm.Name}> for background job <{HiveLog.Job.Id}>", job.Id);
                        yield return new MiddlewareInfo(middleware, hiveOptions, cache);
                    }
                }

                currentSwarm = currentSwarm.Parent;
            }
            while (useFromParentSwarm && currentSwarm != null);
        }
        private async Task<(IBackgroundJobMiddleware Middleware, IMiddlewareInfo Info)[]> ActiveMiddleware(IActivatorScope activatorScope, IEnumerable<IMiddlewareInfo> middleware)
        {
            activatorScope.ValidateArgument(nameof(activatorScope));
            middleware.ValidateArgument(nameof(middleware));
            var jobMiddleware = new List<(IBackgroundJobMiddleware Middleware, IMiddlewareInfo Info)>();

            foreach (var info in middleware)
            {
                jobMiddleware.Add(((await activatorScope.Active(info.Type).ConfigureAwait(false)).CastTo<IBackgroundJobMiddleware>(), info));
            }

            return jobMiddleware.ToArray();
        }

        private (Func<object, object[], object> InvocationDelegate, int? JobContextIndex, int? CancellationTokenIndex) GetJobInvocationInfo(IDaemonExecutionContext context, HiveMindOptions hiveMindOptions, IMemoryCache memoryCache, IInvocationInfo invocationInfo)
        {
            hiveMindOptions.ValidateArgument(nameof(hiveMindOptions));
            memoryCache.ValidateArgument(nameof(memoryCache));
            invocationInfo.ValidateArgument(nameof(invocationInfo));

            var method = invocationInfo.MethodInfo ?? throw new InvalidOperationException($"Could not get method info to execute from <{invocationInfo}>. Did method signiture change?");
            var methodFullName = $"{invocationInfo.Type.FullName}.{invocationInfo.MethodInfo.GetDisplayName(MethodDisplayOptions.Full)}";
            var cacheKey = $"{hiveMindOptions.CachePrefix}.JobInvocation.{methodFullName}";

            using var durationScope = Helper.Time.CaptureDuration(x => context.Log(LogLevel.Debug, $"Got invocation info for <{methodFullName}> in <{x.PrintTotalMs()}>"));
            return memoryCache.GetOrCreate<(Func<object, object[], object> InvocationDelegate, int? JobContextIndex, int? CancellationTokenIndex)>(cacheKey, x =>
            {
                x.SlidingExpiration = hiveMindOptions.DelegateExpiryTime;
                context.Log(LogLevel.Debug, $"Generating invocation delegate for <{methodFullName}>");

                int? jobContextIndex = null;
                int? cancellationTokenIndex = null;

                var bodyExpressions = new List<LinqExpression>();
                // Parameters
                var instanceParameter = LinqExpression.Parameter(typeof(object), "i");
                var argumentParameter = LinqExpression.Parameter(typeof(object[]), "a");

                // We need to cast instance if method is not static
                ParameterExpression instanceVariable = null;
                if (!invocationInfo.MethodInfo.IsStatic)
                {
                    instanceVariable = LinqExpression.Variable(typeof(object), "instance");
                    var instanceVariableCast = LinqExpression.Assign(instanceVariable, LinqExpression.Convert(instanceParameter, invocationInfo.Type));
                    bodyExpressions.Add(instanceVariableCast);
                }

                // Method call
                var methodParameters = new List<LinqExpression>();
                foreach (var methodParameter in invocationInfo.MethodInfo.GetParameters())
                {
                    // Access method argument from parameters
                    var arrayAccessExpression = LinqExpression.ArrayAccess(argumentParameter, LinqExpression.Constant(methodParameter.Position));
                    // Cast array element to method parameter type
                    methodParameters.Add(LinqExpression.Convert(arrayAccessExpression, methodParameter.ParameterType));

                    if (methodParameter.ParameterType.Is<IBackgroundJobExecutionContext>()) jobContextIndex = methodParameter.Position;
                    else if (methodParameter.ParameterType.Is<CancellationToken>()) cancellationTokenIndex = methodParameter.Position;
                }
                LinqExpression methodCall = instanceVariable != null ? LinqExpression.Call(instanceVariable, invocationInfo.MethodInfo, methodParameters) : LinqExpression.Call(invocationInfo.MethodInfo, methodParameters);
                if (invocationInfo.MethodInfo.ReturnType.IsValueType && !invocationInfo.MethodInfo.ReturnType.Equals(typeof(void)))
                {
                    // Need explicit casting for value types
                    methodCall = LinqExpression.Convert(methodCall, typeof(object));
                }
                bodyExpressions.Add(methodCall);

                // Return null if method return is void
                if (invocationInfo.MethodInfo.ReturnType.Equals(typeof(void)))
                {
                    bodyExpressions.Add(LinqExpression.Constant(null, typeof(object)));
                }

                // Create lambda body
                var body = instanceVariable != null ? LinqExpression.Block(instanceVariable.AsArray(), bodyExpressions) : LinqExpression.Block(bodyExpressions);

                // Create lambda
                return (LinqExpression.Lambda<Func<object, object[], object>>(body, instanceParameter, argumentParameter).Compile(), jobContextIndex, cancellationTokenIndex);
            });
        }

        private async Task ExecuteJobAsync(IDaemonExecutionContext context, IDroneState<WorkerSwarmHostOptions> state, IBackgroundJobExecutionContext jobExecutionContext, Func<object, object[], object> invocationDelegate, int? jobContextIndex, int? cancellationTokenIndex, (IBackgroundJobMiddleware Middleware, IMiddlewareInfo Info)[] jobMiddleware, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            jobExecutionContext.ValidateArgument(nameof(jobExecutionContext));
            invocationDelegate.ValidateArgument(nameof(invocationDelegate));
            jobMiddleware.ValidateArgument(nameof(jobMiddleware));

            context.Log(LogLevel.Debug, $"Drone <{state.FullName}> invoking background job <{HiveLog.Job.Id}> with <{jobMiddleware.Length}> middleware", jobExecutionContext.Job.Id);
            var currentMiddleware = 0;
            Func<IBackgroundJobExecutionContext, CancellationToken, Task> next = null;
            next = new Func<IBackgroundJobExecutionContext, CancellationToken, Task>(async (c, t) =>
            {
                c.ValidateArgument(nameof(c));

                // Middleware
                if (currentMiddleware < jobMiddleware.Length)
                {
                    var index = currentMiddleware;
                    currentMiddleware++;
                    var (middelware, info) = jobMiddleware[index];

                    context.Log(LogLevel.Debug, $"Drone <{state.FullName}> invoking middleware <{index}> of type <{middelware.GetType()}> for background job <{HiveLog.Job.Id}>", jobExecutionContext.Job.Id);
                    await middelware.ExecuteAsync(c, info.Context, next, token);
                    context.Log(LogLevel.Debug, $"Drone <{state.FullName}> invoked middleware <{index}> of type <{middelware.GetType()}> for background job <{HiveLog.Job.Id}>", jobExecutionContext.Job.Id);
                }
                // Invoke job
                else
                {
                    context.Log(LogLevel.Debug, $"Drone <{state.FullName}> invoking background job <{HiveLog.Job.Id}>", jobExecutionContext.Job.Id);

                    // Replace special arguments
                    if (jobContextIndex.HasValue) c.InvocationArguments[jobContextIndex.Value] = c;
                    if (cancellationTokenIndex.HasValue) c.InvocationArguments[cancellationTokenIndex.Value] = t;

                    try
                    {
                        using (Helper.Time.CaptureDuration(x => c.Duration = x))
                        {
                            var result = invocationDelegate(c.JobInstance, c.InvocationArguments);

                            // Await result if async
                            if (result is ValueTask valueTask)
                            {
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.Id}> is asynchronous. Drone <{state.FullName}> awaiting task", jobExecutionContext.Job.Id);
                                await valueTask.ConfigureAwait(false);
                                c.Result = null;
                            }
                            else if (result is Task<object> objectTask)
                            {
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.Id}> is asynchronous. Drone <{state.FullName}> awaiting task", jobExecutionContext.Job.Id);
                                c.Result = await objectTask.ConfigureAwait(false);
                            }
                            else if (result is Task task)
                            {
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.Id}> is asynchronous. Drone <{state.FullName}> awaiting task", jobExecutionContext.Job.Id);
                                await task.ConfigureAwait(false);
                                c.Result = null;

                                // Check if task has result
                                var taskType = task.GetType();
                                if (taskType.IsGenericType && taskType.GetGenericTypeDefinition().Equals(typeof(Task<>)) && !taskType.Equals(VoidTaskType))
                                {
                                    c.Result = task.CastTo<dynamic>().GetAwaiter().GetResult();
                                }
                            }
                        }
                        context.Log(LogLevel.Debug, $"Drone <{state.FullName}> invoked background job <{HiveLog.Job.Id}>", jobExecutionContext.Job.Id);
                    }
                    catch (Exception ex)
                    {
                        context.Log(LogLevel.Warning, $"Drone <{state.FullName}> received exception from invoking background job <{HiveLog.Job.Id}>", ex, jobExecutionContext.Job.Id);
                        c.Result = ex;
                    }
                }
            });

            await next(jobExecutionContext, token);
        }

        /// <inheritdoc/>
        protected override async Task HandleErrorAsync(IDaemonExecutionContext context, IDroneState<WorkerSwarmHostOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, Exception exception, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));
            exception.ValidateArgument(nameof(exception));

            await backgroundJob.ChangeStateAsync(new FailedState(exception)
            {
                Reason = $"Exception was thrown while drone <{state.FullName}> was handling job"
            }, token).ConfigureAwait(false);
            await backgroundJob.SaveChangesAsync(false, token).ConfigureAwait(false);
            await job.CompleteAsync(token).ConfigureAwait(false);
        }
    }
}
