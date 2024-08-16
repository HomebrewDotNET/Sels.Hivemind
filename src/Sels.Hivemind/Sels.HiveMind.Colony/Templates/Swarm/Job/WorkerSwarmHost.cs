using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Validators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Sels.HiveMind.HiveLog;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// Base class for creating a swarm that processes <see cref="ILockedJob{TLockedJob, TChangeTracker, TState, TAction}"/> placed in queues of a supplied queue type by executing them.
    /// </summary>
    /// <typeparam name="TWriteableOptions"><typeparamref name="TOptions"/> but editable</typeparam>
    /// <typeparam name="TExecutionContext">The execution context that is used when executing a job</typeparam>
    /// <typeparam name="TMiddlware">The type of middleware used when processing jobs</typeparam>
    /// <typeparam name="TReadOnlyJob">The type of the readonly version of the jobs to execute</typeparam>
    /// <typeparam name="TWriteableJob">The type of the writable version of the jobs to execute</typeparam>
    /// <typeparam name="TLockedJob">The type of the locked version of the jobs to execute</typeparam>
    /// <typeparam name="TSortTarget">The type used to determine the sort order when querying the jobs</typeparam>
    /// <typeparam name="TOptions">The type of options used by this host</typeparam>
    /// <typeparam name="TDefaultOptions">The type of default options used by this host</typeparam>
    /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
    public abstract class WorkerSwarmHost<TWriteableOptions, TExecutionContext, TMiddlware, TWriteableJob, TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions> : JobSwarmHost<TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions>
        where TWriteableOptions : TOptions
        where TExecutionContext : IJobExecutionContext<TWriteableJob>, IAsyncDisposable
        where TMiddlware : class, IJobMiddleware<TExecutionContext, TWriteableJob>
        where TOptions : IWorkerSwarmHostOptions<TMiddlware, TOptions>
        where TDefaultOptions : WorkerSwarmDefaultHostOptions
        where TReadOnlyJob : class, IReadOnlyJob<TLockedJob, TChangeTracker, TState, TAction>
        where TWriteableJob : IWriteableJob<TLockedJob, TChangeTracker, TState, TAction>, TReadOnlyJob
        where TLockedJob : ILockedJob<TLockedJob, TChangeTracker, TState, TAction>, TWriteableJob
        where TState : IJobState
        where TChangeTracker : IJobChangeTracker<TState>
    {
        /// <inheritdoc cref="WorkerSwarmHost{TReadOnlyOptions, TExecutionContext, TMiddlware, TWriteableJob, TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions}"/>
        /// <param name="initialOptions">The initial options used by the current swarm</param>
        /// <param name="client"><inheritdoc cref="JobSwarmHost{TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions}._client"/></param>
        /// <param name="queueType"><inheritdoc cref="SwarmHost{TOptions, TDefaultOptions}.QueueType"/></param>
        /// <param name="defaultOptions"><inheritdoc cref="SwarmHost{TOptions, TDefaultOptions}._defaultOptions"/></param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        /// <param name="scheduleBuilder"><inheritdoc cref="ScheduledDaemon.Schedule"/></param>
        /// <param name="scheduleBehaviour"><inheritdoc cref="ScheduledDaemon.Behaviour"/></param>
        /// <param name="taskManager"><inheritdoc cref="ScheduledDaemon._taskManager"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="ScheduledDaemon._calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="ScheduledDaemon._intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="ScheduledDaemon._hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="ScheduledDaemon._cache"/></param>
        /// <param name="logger"><inheritdoc cref="ScheduledDaemon._logger"/></param>
        protected WorkerSwarmHost(TWriteableOptions initialOptions, IJobClient<TReadOnlyJob, TLockedJob, TSortTarget> client, string queueType, IOptionsMonitor<TDefaultOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
        : base(client, queueType, defaultOptions, jobQueueProvider, schedulerProvider, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            SetOptions(Guard.IsNotNull(initialOptions));
        }

        // Statics
        private static Type VoidTaskType = typeof(Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult", true)!);

        // Fields
        private readonly object _lock = new object();

        // State
        private TWriteableOptions _currentOptions;

        // Properties
        /// <inheritdoc/>
        protected override TOptions Options { get { lock (_lock) { return _currentOptions; } } }
        /// <summary>
        /// The current options being used by the swarm host.
        /// </summary>
        public TOptions CurrentOptions => Options;
        /// <inheritdoc/>
        protected override string SwarmPrefix => $"Worker.";

        /// <summary>
        /// Overwrites the options for this instance. Host will restart.
        /// </summary>
        /// <param name="options"><inheritdoc cref="Options"/></param>
        public void SetOptions(TWriteableOptions options)
        {
            options.ValidateArgument(nameof(options));
            ValidateAndThrowIfInvalid(options);

            lock (_lock)
            {
                _currentOptions = options;
                if (State != ScheduledDaemonState.Starting)
                {
                    _logger.Debug("Options changed. Sending stop signal to restart daemon");
                    SignalStop();
                }
            }
        }

        /// <inheritdoc/>
        public override async Task Execute(IDaemonExecutionContext context, CancellationToken token)
        {
            context.Log($"Worker swarm <{HiveLog.Daemon.NameParam}> setting up", context.Daemon.Name);

            // Monitoring for changes and restart if changes are detected
            using var defaultOptionsMonitorRegistration = _defaultOptions.OnChange((o, n) =>
            {
                if (n.EqualsNoCase(context.Daemon.Colony.Environment))
                {
                    context.Log(LogLevel.Debug, "Default options changed. Sending stop signal to restart");
                    SignalStop();
                }
            });

            if (token.IsCancellationRequested) return;
            await base.Execute(context, token);
        }
        /// <summary>
        /// Validates <paramref name="options"/> and should throw an exception when it's not.
        /// </summary>
        /// <param name="options">The instance to validate</param>
        protected abstract void ValidateAndThrowIfInvalid(TWriteableOptions options);

        /// <inheritdoc/>
        protected override async Task ProcessJobAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob dequeuedJob, TLockedJob job, HiveMindOptions options, CancellationTokenSource jobTokenSource, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            dequeuedJob.ValidateArgument(nameof(dequeuedJob));
            job.ValidateArgument(nameof(job));
            jobTokenSource.ValidateArgument(nameof(jobTokenSource));
            options.ValidateArgument(nameof(options));

            // Activate job and middleware to execute
            await using var serviceProviderScope = context.ServiceProvider.CreateAsyncScope();
            var serviceProvider = serviceProviderScope.ServiceProvider;
            var environment = context.Daemon.Colony.Environment;
            var taskManager = serviceProvider.GetRequiredService<ITaskManager>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var activator = serviceProvider.GetRequiredService<IActivator>();
            await using var activatorScope = await activator.CreateActivatorScopeAsync(serviceProvider, token).ConfigureAwait(false);
            var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
            var (invocationDelegate, jobContextIndex, cancellationTokenIndex) = GetJobInvocationInfo(context, options, memoryCache, job.Invocation);
            object? instance = null;
            if (!job.Invocation.MethodInfo.IsStatic) instance = await activatorScope.ActivateAsync(job.Invocation.Type, token).ConfigureAwait(false);
            var middleware = await ActivateMiddleware(serviceProvider, activatorScope, GetMiddleware(context, activatorScope, options, state, job, memoryCache, token), options, _cache, token).ConfigureAwait(false);

            try
            {
                // Get storage
                var storageProvider = serviceProvider.GetRequiredService<IStorageProvider>();
                await using var storageScope = await storageProvider.CreateAsync(environment, token).ConfigureAwait(false);
                var storage = storageScope.Component;

                // Create execution context
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                await using (var executionContext = CreateContext(context,
                                                                  state,
                                                                  serviceProvider,
                                                                  job,
                                                                  instance,
                                                                  job.Invocation.Arguments?.ToArray() ?? Array.Empty<object>(),
                                                                  jobTokenSource,
                                                                  CurrentOptions.LogLevel ?? _defaultOptions.CurrentValue.LogLevel,
                                                                  CurrentOptions.LogFlushInterval ?? _defaultOptions.CurrentValue.LogFlushInterval,
                                                                  CurrentOptions.ActionPollingInterval ?? _defaultOptions.CurrentValue.ActionPollingInterval,
                                                                  CurrentOptions.ActionFetchLimit ?? _defaultOptions.CurrentValue.ActionFetchLimit,
                                                                  activatorScope,
                                                                  taskManager,
                                                                  storage,
                                                                  loggerFactory))
                {
                    await ExecuteJobAsync(context, state, serviceProvider, executionContext, dequeuedJob, job, async () =>
                    {
                        await InvokeJobAsync(context, state, executionContext, invocationDelegate!, jobContextIndex, cancellationTokenIndex, middleware.Select(x => (x.Middleware.Component, x.Info)).ToArray(), token).ConfigureAwait(false);
                    }, stopwatch, jobTokenSource, activatorScope, taskManager, storage, loggerFactory, token).ConfigureAwait(false);
                }
            }
            finally
            {
                await middleware.Select(x => x.Middleware).ForceExecuteAsync(x => x.DisposeAsync().AsTask(), (m, e) => _logger.Log($"Failed to dispose middleware <{m}>", e)).ConfigureAwait(false);
            }            
        }

        /// <summary>
        /// Creates the execution context that will be used to process <paramref name="job"/>.
        /// </summary>
        /// <param name="context">The context of the daemon running the current swarm</param>
        /// <param name="state">The state of the drone that's currently processing</param>
        /// <param name="serviceProvider">The service provider that can be used to resolve dependencies</param>
        /// <param name="job">The job going to be processed</param>
        /// <param name="instance">The instance to invoke to execute <paramref name="job"/>. Can be null when invoking a static method</param>
        /// <param name="arguments">The arguments that will be used when invoking the method to run <paramref name="job"/></param>
        /// <param name="tokenSource">The cancellation token source that is used to control the cancelletion of the execution of <paramref name="job"/></param>
        /// <param name="logLevel">The minimum log level enabled above which to persists logs created by executing <paramref name="job"/></param>
        /// <param name="logFlushInterval">How often logs created by executing <paramref name="job"/> should be persisted to storage</param>
        /// <param name="actionPollingInterval">How often to check for pending action when executing <paramref name="job"/></param>
        /// <param name="actionFetchLimit">How many actions can be fetched in one go</param>
        /// <param name="activatorScope">The activor scope that was opened to resolve any dependencies for executing <paramref name="job"/></param>
        /// <param name="taskManager">Can be used to manage <see cref="Task"/>(s)</param>
        /// <param name="storage">The storage used to manage the state for <paramref name="job"/></param>
        /// <param name="loggerFactory">Optional logger factory that can be used to create new loggers</param>
        /// <returns>The execution context that will be used to execute <paramref name="job"/></returns>
        protected abstract TExecutionContext CreateContext(IDaemonExecutionContext context, IDroneState<TOptions> state, IServiceProvider serviceProvider, TLockedJob job, object? instance, object[] arguments, CancellationTokenSource tokenSource, LogLevel logLevel, TimeSpan logFlushInterval, TimeSpan actionPollingInterval, int actionFetchLimit, IActivatorScope activatorScope, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory);

        /// <summary>
        /// Executes <paramref name="lockedJob"/>.
        /// </summary>
        /// <param name="context">The context of the daemon running the current swarm</param>
        /// <param name="state">The state of the drone that's currently processing</param>
        /// <param name="serviceProvider">The service provider that can be used to resolve dependencies</param>
        /// <param name="jobExecutionContext">The execution context for <paramref name="lockedJob"/></param>
        /// <param name="dequeuedJob">The job that was retrieved</param>
        /// <param name="lockedJob">The job that will be executed</param>
        /// <param name="executeJob">Delegate that is called to execute <paramref name="lockedJob"/>. The result will be stored in <paramref name="jobExecutionContext"/></param>
        /// <param name="stopwatch">The stopwatch that was started before the pre processing started for <paramref name="lockedJob"/></param>
        /// <param name="jobTokenSource">The cancellation token source that is used to control the cancelletion of the execution of <paramref name="lockedJob"/></param>
        /// <param name="activatorScope">The activor scope that was opened to resolve any dependencies for executing <paramref name="job"/></param>
        /// <param name="taskManager">Can be used to manage <see cref="Task"/>(s)</param>
        /// <param name="storage">The storage used to manage the state for <paramref name="lockedJob"/></param>
        /// <param name="loggerFactory">Optional logger factory that can be used to create new loggers</param>
        /// <param name="token">Token that will be called when the current method is requested to stop</param>
        protected abstract Task ExecuteJobAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IServiceProvider serviceProvider, TExecutionContext jobExecutionContext, IDequeuedJob dequeuedJob, TLockedJob lockedJob, Func<Task> executeJob, Stopwatch stopwatch, CancellationTokenSource jobTokenSource, IActivatorScope activatorScope, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory, CancellationToken token);

        private IEnumerable<ISwarmHostMiddlewareOptions<TMiddlware>> GetMiddleware(IDaemonExecutionContext context, IActivatorScope activatorScope, HiveMindOptions hiveOptions, IDroneState<TOptions> state, TReadOnlyJob job, IMemoryCache? cache, CancellationToken cancellationToken)
        {
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            activatorScope.ValidateArgument(nameof(activatorScope));
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));

            if (job.Middleware.HasValue())
            {
                foreach (var jobMiddleware in job.Middleware)
                {
                    context.Log($"Drone <{HiveLog.Swarm.NameParam}> got middleware <{jobMiddleware.Type}> with priority <{jobMiddleware.Priority}> from job itself for background job <{HiveLog.Job.IdParam}>", state.FullName, job.Id);
                    yield return new SwarmHostMiddlewareOptions<TMiddlware>()
                    {
                        Context = jobMiddleware.Context,
                        Factory = async x =>
                        {
                            var activatedMiddleware = (await activatorScope.ActivateAsync(jobMiddleware.Type).ConfigureAwait(false)).CastTo<TMiddlware>();
                            return new ScopedComponent<TMiddlware>(activatedMiddleware.GetTypeName(), activatedMiddleware, null, false);
                        },
                        ConfigurationOptions = new SwarmHostMiddlewareConfigurationOptions()
                        {
                            Priority = jobMiddleware.Priority
                        }
                    };
                }
            }

            ISwarmState<TOptions>? currentSwarm = state.Swarm;

            // Determine current depth level of swarm
            var depth = 0;
            var parent = currentSwarm.Parent;
            while (parent != null)
            {
                depth++;
                parent = parent.Parent;
            }

            // Loop over swarm options until we reach the root or the first dedicated swarm
            var currentDepth = depth;
            Dictionary<int, ISwarmHostMiddlewareOptions<TMiddlware>[]> middlewarePerDepth = new Dictionary<int, ISwarmHostMiddlewareOptions<TMiddlware>[]>();
            bool isDedicated = currentSwarm.Options.IsDedicated;
            var middleware = new List<ISwarmHostMiddlewareOptions<TMiddlware>>();   
            do
            {
                var definedMiddleware = currentSwarm.Options.JobMiddleware;
                if (definedMiddleware.HasValue())
                {
                    var isDroneSwarm = state.Swarm.Equals(currentSwarm);
                    middlewarePerDepth.Add(currentDepth, definedMiddleware!.ToArray());

                    if (isDroneSwarm)
                    {
                        foreach(var exclusiveMiddlware in definedMiddleware!.Where(x => x.ConfigurationOptions.InheritanceBehaviour.HasFlag(SwarmMiddlewareInheritanceBehaviour.Exclusive)))
                        {
                            context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> got exclusive middleware <{exclusiveMiddlware}> from it's own swarm <{HiveLog.Swarm.NameParam}> for background job <{HiveLog.Job.IdParam}>", state.FullName, state.Swarm.Name, job.Id);
                            middleware.Add(exclusiveMiddlware);
                        }
                    }

                    // Check if current job is from swarm
                    if(currentSwarm.Options.Queues.HasValue() && currentSwarm.Options!.Queues!.Select(x => x.Name).Contains(job.Queue, StringComparer.OrdinalIgnoreCase))
                    {
                        // Found swarm job is from so return all middleware                       
                        
                        // Check which middleware we can inherit from child swarms
                        foreach (var (middlewareDepth, childMiddleware) in middlewarePerDepth.Where(x => x.Key > currentDepth))
                        {
                            foreach (var potentialMiddleware in childMiddleware)
                            {
                                switch (potentialMiddleware.ConfigurationOptions.InheritanceBehaviour)
                                {
                                    case SwarmMiddlewareInheritanceBehaviour.Exclusive:
                                        continue;
                                    case SwarmMiddlewareInheritanceBehaviour.Inherit:
                                        // Inheritance is enabled. Check if there are depth limits configured
                                        var depthLimit = potentialMiddleware.ConfigurationOptions.MaxInheritanceDepth;
                                        var depthDifference = middlewareDepth - currentDepth;
                                        if (!depthLimit.HasValue || depthDifference <= depthLimit.Value)
                                        {
                                            middleware.Add(potentialMiddleware);
                                        }
                                        break;
                                    default: throw new NotSupportedException($"Inheritance behaviour <{potentialMiddleware.ConfigurationOptions.InheritanceBehaviour}> is not supported");
                                }
                            }
                        }

                        context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> got middleware <{definedMiddleware!.Count}> from swarm <{HiveLog.Swarm.NameParam}> for background job <{HiveLog.Job.IdParam}>", state.FullName, state.Swarm.Name, job.Id);
                        foreach(var swarmMiddleware in definedMiddleware)
                        {
                            yield return swarmMiddleware;                           
                        }
                        currentSwarm = null;
                    }         
                }
                currentDepth--;
                isDedicated = currentSwarm?.Options?.IsDedicated ?? true;
                currentSwarm = currentSwarm?.Parent;
            }
            while (currentSwarm != null && !isDedicated);
        }
        private async Task<(IComponent<TMiddlware> Middleware, IMiddlewareInfo Info)[]> ActivateMiddleware(IServiceProvider serviceProvider, IActivatorScope activatorScope, IEnumerable<ISwarmHostMiddlewareOptions<TMiddlware>> middleware, HiveMindOptions hiveOptions, IMemoryCache? cache, CancellationToken cancellationToken)
        {
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            activatorScope.ValidateArgument(nameof(activatorScope));
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            middleware.ValidateArgument(nameof(middleware));
            var jobMiddleware = new List<(IComponent<TMiddlware> Middleware, IMiddlewareInfo Info)>();

            try
            {
                foreach (var swarmMiddleware in middleware)
                {
                    if (swarmMiddleware.Data != null)
                    {
                        var info = new MiddlewareInfo(swarmMiddleware.Data, hiveOptions, cache);
                        var activatedMiddleware = (await activatorScope.ActivateAsync(info.Type, cancellationToken).ConfigureAwait(false)).CastTo<TMiddlware>();
                        activatedMiddleware.Priority = swarmMiddleware.ConfigurationOptions.Priority;
                        jobMiddleware.Add((new ScopedComponent<TMiddlware>(activatedMiddleware.GetTypeName(), activatedMiddleware, null, false), info));
                    }
                    else if (swarmMiddleware.Factory != null)
                    {
                        var activatedMiddleware = (await swarmMiddleware.Factory(serviceProvider).ConfigureAwait(false));
                        activatedMiddleware.Component.Priority = swarmMiddleware.ConfigurationOptions.Priority;
                        jobMiddleware.Add((activatedMiddleware, new MiddlewareInfo(activatedMiddleware.GetType(), swarmMiddleware.Context, swarmMiddleware.ConfigurationOptions.Priority, hiveOptions, cache)));
                    }
                }
            }
            catch (Exception)
            {
                await jobMiddleware.Select(x => x.Middleware).ForceExecuteAsync(x => x.DisposeAsync().AsTask(), (m, e) => _logger.Log($"Failed to dispose middleware <{m}>", e)).ConfigureAwait(false);
                throw;
            }

            return jobMiddleware.OrderByDescending(x => x.Middleware.Component.Priority.HasValue).OrderBy(x => x.Middleware.Component.Priority ?? (byte)0).ToArray();
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
                ParameterExpression? instanceVariable = null;
                if (!invocationInfo.MethodInfo.IsStatic)
                {
                    instanceVariable = LinqExpression.Variable(invocationInfo.Type, "instance");
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

                    if (methodParameter.ParameterType.Is<TExecutionContext>()) jobContextIndex = methodParameter.Position;
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

        /// <summary>
        /// Invokes the method to execute the job in <paramref name="jobExecutionContext"/> with any defined middleware.
        /// </summary>
        /// <param name="context">The context of the daemon running the current swarm</param>
        /// <param name="state">The state of the drone that's currently processing</param>
        /// <param name="jobExecutionContext">THe execution context including the job that will be invoked</param>
        /// <param name="invocationDelegate">The delegate that will call the method to execute the job</param>
        /// <param name="jobContextIndex">The index of the method argument that is equal to the context. Used replace the argument with <paramref name="jobExecutionContext"/>. Null if not defined on the method</param>
        /// <param name="cancellationTokenIndex">The index of the method argument that is equal to a cancellation token. Used replace the argument with the cancellation token in <paramref name="jobExecutionContext"/>. Null if not defined on the method</param>
        /// <param name="jobMiddleware">Array with the middleware to execute before invoking the job</param>
        /// <param name="token">Token that can be cancelled to cancel the execution of the job</param>
        protected async Task InvokeJobAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, TExecutionContext jobExecutionContext, Func<object?, object[], object> invocationDelegate, int? jobContextIndex, int? cancellationTokenIndex, (TMiddlware Middleware, IMiddlewareInfo Info)[] jobMiddleware, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            jobExecutionContext.ValidateArgument(nameof(jobExecutionContext));
            invocationDelegate.ValidateArgument(nameof(invocationDelegate));
            jobMiddleware.ValidateArgument(nameof(jobMiddleware));

            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> invoking job <{HiveLog.Job.IdParam}> with <{jobMiddleware.Length}> middleware", state.FullName, jobExecutionContext.Job.Id);
            var currentMiddleware = 0;
            Func<TExecutionContext, CancellationToken, Task>? next = null;
            next = new Func<TExecutionContext, CancellationToken, Task>(async (c, t) =>
            {
                c.ValidateArgument(nameof(c));

                // Middleware
                if (currentMiddleware < jobMiddleware.Length)
                {
                    var index = currentMiddleware;
                    currentMiddleware++;
                    var (middelware, info) = jobMiddleware[index];

                    context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> invoking middleware <{index}> of type <{middelware.GetType()}> for job <{HiveLog.Job.IdParam}>", state.FullName, jobExecutionContext.Job.Id);
                    await middelware.ExecuteAsync(c, info.Context, next!, token);
                    context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> invoked middleware <{index}> of type <{middelware.GetType()}> for job <{HiveLog.Job.IdParam}>", state.FullName, jobExecutionContext.Job.Id);
                }
                // Invoke job
                else
                {
                    context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> invoking job <{HiveLog.Job.IdParam}>", state.FullName, jobExecutionContext.Job.Id);

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
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.IdParam}> is asynchronous. Drone <{HiveLog.Swarm.NameParam}> awaiting task", jobExecutionContext.Job.Id, state.FullName);
                                await valueTask.ConfigureAwait(false);
                                c.Result = null;
                            }
                            else if (result is Task<object> objectTask)
                            {
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.IdParam}> is asynchronous. Drone <{HiveLog.Swarm.NameParam}> awaiting task", jobExecutionContext.Job.Id, state.FullName);
                                c.Result = await objectTask.ConfigureAwait(false);
                            }
                            else if (result is Task task)
                            {
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.IdParam}> is asynchronous. Drone <{HiveLog.Swarm.NameParam}> awaiting task", jobExecutionContext.Job.Id, state.FullName);
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
                        context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> invoked background job <{HiveLog.Job.IdParam}>", state.FullName, jobExecutionContext.Job.Id);
                    }
                    catch (OperationCanceledException cancelEx) when (token.IsCancellationRequested)
                    {
                        context.Log($"Background job <{HiveLog.Job.IdParam}> was cancelled while being executed by drone <{HiveLog.Swarm.NameParam}", jobExecutionContext.Job.Id, state.FullName);
                        c.Result = cancelEx;
                    }
                    catch (Exception ex)
                    {
                        context.Log(LogLevel.Warning, $"Drone <{HiveLog.Swarm.NameParam}> received exception from invoking background job <{HiveLog.Job.IdParam}>", ex, state.FullName, jobExecutionContext.Job.Id);
                        c.Result = ex;
                    }
                }
            });

            await next(jobExecutionContext, token);
        }
    }
}
