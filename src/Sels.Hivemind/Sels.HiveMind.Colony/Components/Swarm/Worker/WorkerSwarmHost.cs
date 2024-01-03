﻿using Microsoft.Extensions.Caching.Memory;
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
using Sels.HiveMind.Colony.Swarm;
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
using static Sels.HiveMind.HiveMindConstants;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Sels.HiveMind.Colony.Swarm.Worker
{
    /// <summary>
    /// A swarm host that executes dequeued jobs.
    /// </summary>
    public class WorkerSwarmHost : SwarmHost<WorkerSwarmHostOptions>
    {
        // Statics
        private static Type VoidTaskType = typeof(Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult"));

        // Fields
        private readonly object _lock = new object();
        private readonly IOptionsMonitor<WorkerSwarmDefaultHostOptions> _defaultWorkerOptions;

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

        /// <inheritdoc cref="WorkerSwarmHost"/>
        /// <param name="defaultWorkerOptions">The default worker options for this swarm</param>
        /// <param name="defaultOptions"><inheritdoc cref="_defaultOptions"/></param>
        /// <param name="taskManager">Used to manage dromes</param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        public WorkerSwarmHost(WorkerSwarmHostOptions options, IOptionsMonitor<WorkerSwarmDefaultHostOptions> defaultWorkerOptions, IOptionsMonitor<SwarmHostDefaultOptions> defaultOptions, ITaskManager taskManager, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider) : base(HiveMindConstants.Queue.BackgroundJobProcessQueueType, defaultOptions, taskManager, jobQueueProvider, schedulerProvider)
        {
            _defaultWorkerOptions = defaultWorkerOptions.ValidateArgument(nameof(defaultWorkerOptions));
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
            while(!token.IsCancellationRequested)
            {
                var swarmTokenSource = new CancellationTokenSource();

                try
                {
                    // Monitoring for changes and restart if changes are detected
                    using var tokenSourceRegistration = token.Register(() => swarmTokenSource.Cancel());
                    using var defaultOptionsMonitorRegistration = _defaultWorkerOptions.OnChange((o, n) =>
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
                    continue;
                }
                catch(Exception)
                {
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task ProcessAsync(IDaemonExecutionContext context, IDroneState<WorkerSwarmHostOptions> state, IServiceProvider serviceProvider, IDequeuedJob job, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            var client = serviceProvider.GetRequiredService<IBackgroundJobClient>();
            var environment = context.Daemon.Colony.Environment;

            context.Log($"Drone <{state.FullName}> received background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> to process", job.JobId, environment);

            // Fetch job
            context.Log(LogLevel.Debug, $"Drone <{state.FullName}> fetching background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for reading", job.JobId, environment);
            IReadOnlyBackgroundJob readOnlyBackgroundJob = await FetchBackgroundJob(context, client, state, job, token).ConfigureAwait(false);
            if (readOnlyBackgroundJob == null) return;
            await using var readOnlyBackgroundJobScope = readOnlyBackgroundJob;
            context.Log(LogLevel.Debug, $"Drone <{state.FullName}> fetched background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for reading. Checking state", job.JobId, environment);

            // Check if job can be processed
            if (!CanBeProcessed(context, job, readOnlyBackgroundJob))
            {
                context.Log(LogLevel.Error, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be processed. Dropping dequeued job", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                await job.CompleteAsync(token).ConfigureAwait(false);
                return;
            }

            // Check if job can be locked
            context.Log(LogLevel.Debug, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> can be processed by drone <{state.FullName}>. Checking if it can be locked", job.JobId, environment);
            var hiveOptions = serviceProvider.GetRequiredService<IOptionsMonitor<HiveMindOptions>>();
            if (readOnlyBackgroundJob.IsLocked && !readOnlyBackgroundJob.Lock.LockedBy.EqualsNoCase(GetLockRequester(state)))
            {
                var delay = state.Swarm.Options.LockedDelay ?? _defaultWorkerOptions.CurrentValue.LockedDelay;
                var delayToDate = DateTime.UtcNow.Add(delay);
                context.Log(LogLevel.Error, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is already locked. Dequeued job will be delayed to <{delayToDate}>", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                await job.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                return;
            }

            // Lock job and check again just to be sure
            context.Log(LogLevel.Debug, $"Drone <{state.FullName}> attempting to lock background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", job.JobId, environment);
            await using var backgroundJob = await readOnlyBackgroundJob.LockAsync(GetLockRequester(state), token).ConfigureAwait(false);
            if (!CanBeProcessed(context, job, backgroundJob))
            {
                context.Log(LogLevel.Error, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be processed after acquiring lock. Dropping dequeued job", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                await job.CompleteAsync(token).ConfigureAwait(false);
                return;
            }
            context.Log(LogLevel.Debug, $"Drone <{state.FullName}> got lock on background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>. Starting keep alive task for lock", job.JobId, environment);

            // Start keep alive task to keep lock on background job alive
            var taskManager = serviceProvider.GetRequiredService<ITaskManager>();
            var cancellationTokenSource = new CancellationTokenSource();
            using var tokenRegistration = token.Register(() => cancellationTokenSource.Cancel());
            var keepAliveTask = StartKeepAliveTask(taskManager, context, state, hiveOptions, cancellationTokenSource, backgroundJob, token);
            
            try
            {
                context.Log($"Drone <{state.FullName}> started processing background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", job.JobId, environment);
                // Activate job and middleware to execute
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var activator = serviceProvider.GetRequiredService<IActivator>();
                await using var activatorScope = await activator.CreateActivatorScope(serviceProvider);
                var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
                var (invocationDelegate, jobContextIndex, cancellationTokenIndex) = GetJobInvocationInfo(context, hiveOptions.CurrentValue, memoryCache, backgroundJob.Invocation);
                object instance = null;
                if (!backgroundJob.Invocation.MethodInfo.IsStatic) instance = await activatorScope.Active(backgroundJob.Invocation.Type).ConfigureAwait(false);
                var middleware = await ActiveMiddleware(activatorScope, GetMiddleware(context, hiveOptions.Get(environment), state, backgroundJob, memoryCache)).ConfigureAwait(false);

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
                                                                                     state.Swarm.Options.LogLevel ?? _defaultWorkerOptions.CurrentValue.LogLevel,
                                                                                     state.Swarm.Options.LogFlushInterval ?? _defaultWorkerOptions.CurrentValue.LogFlushInterval,
                                                                                     taskManager,
                                                                                     storage,
                                                                                     loggerFactory?.CreateLogger(backgroundJob.Invocation.Type)))
                {
                    context.Log(LogLevel.Debug, $"Drone <{state.FullName}> setting background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> to executing", job.JobId, environment);
                    // Set to executing
                    var isExecuting = await backgroundJob.ChangeStateAsync(new ExecutingState(context.Daemon.Colony.Name, state.Swarm.Name, state.Name), cancellationTokenSource.Token).ConfigureAwait(false);
                    if(isExecuting)
                    {
                        await backgroundJob.SaveChangesAsync(true, cancellationTokenSource.Token).ConfigureAwait(false);
                        context.Log($"Drone <{state.FullName}> executing background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", job.JobId, environment);
                        
                        // Invoke middleware and job
                        await ExecuteJobAsync(context, state, executionContext, invocationDelegate, jobContextIndex, cancellationTokenIndex, middleware, cancellationTokenSource.Token).ConfigureAwait(false);
                        context.Log($"Drone <{state.FullName}> executed background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> in {stopwatch.Elapsed.PrintTotalMs()}", job.JobId, environment);

                        // Parse result and set final state
                        if (backgroundJob.State.Name.EqualsNoCase(ExecutingState.StateName))
                        {
                            if(executionContext.Result is Exception ex)
                            {
                                context.Log($"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> executed by Drone <{state.FullName}> failed with exception. Setting failed state", ex, job.JobId, environment);
                                await backgroundJob.ChangeStateAsync(new FailedState(ex), cancellationTokenSource.Token).ConfigureAwait(false);
                            }
                            else
                            {
                                stopwatch.Stop();                               
                                await backgroundJob.ChangeStateAsync(new SucceededState(executionContext.Duration, stopwatch.Elapsed, DateTime.UtcNow - backgroundJob.CreatedAtUtc, executionContext.Result), cancellationTokenSource.Token).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not longer in executing state after being executed by Drone <{state.FullName}>. State now is <{backgroundJob.State.Name}>", job.JobId, environment);
                        }
                    }
                    else
                    {
                        context.Log(LogLevel.Warning, $"Drone <{state.FullName}> tried setting background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> to executing but state was transitioned into <{backgroundJob.State.Name}>", job.JobId, environment);
                    }

                    // Save changes and release job
                    await backgroundJob.SaveChangesAsync(false, cancellationTokenSource.Token).ConfigureAwait(false);
                    await job.CompleteAsync(cancellationTokenSource.Token);
                }
            }
            catch(OperationCanceledException cancelledEx) when (token.IsCancellationRequested)
            {
                context.Log(LogLevel.Warning, $"Drone <{state.FullName}> was cancelled while processing background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", cancelledEx, job.JobId, environment);
            }
            catch(Exception ex)
            {
                context.Log(LogLevel.Error, $"Drone <{state.FullName}> could not process background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", ex, job.JobId, environment);
                await backgroundJob.ChangeStateAsync(new FailedState(ex), token).ConfigureAwait(false);
                await backgroundJob.SaveChangesAsync(false, token).ConfigureAwait(false);
                await job.CompleteAsync(token).ConfigureAwait(false);
            }
            finally
            {
                await keepAliveTask.CancelAndWaitOnFinalization(token).ConfigureAwait(false);
            }
        }

        private async Task<IReadOnlyBackgroundJob> FetchBackgroundJob(IDaemonExecutionContext context, IBackgroundJobClient client, IDroneState<WorkerSwarmHostOptions> state, IDequeuedJob job, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            client.ValidateArgument(nameof(client));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));

            var environment = context.Daemon.Colony.Environment;
            var maxCommitTime = state.Swarm.Options.MaxJobCommitTime ?? _defaultWorkerOptions.CurrentValue.MaxJobCommitTime;
            var maxCommitDate = job.EnqueuedAt.Add(maxCommitTime);
            try
            {
                try
                {
                    return await client.GetAndTryLockAsync(context.Daemon.Colony.Environment, job.JobId, GetLockRequester(state), token).ConfigureAwait(false);
                }
                catch (BackgroundJobNotFoundException) when (maxCommitDate > DateTime.Now)
                {
                    var maxWaitTime = state.Swarm.Options.MaxNotFoundWaitTime ?? _defaultWorkerOptions.CurrentValue.MaxNotFoundWaitTime;
                    var waitInterval = state.Swarm.Options.NotFoundCheckInterval ?? _defaultWorkerOptions.CurrentValue.NotFoundCheckInterval;
                    var checkDelay = maxWaitTime / waitInterval;
                    context.Log(LogLevel.Warning, $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> but dequeued job is still within the commit window of <{maxCommitTime}>. Rechecking every <{checkDelay}> for a maximum of <{waitInterval}> times", job.JobId, environment);
                    return await FetchBackgroundJobWithRetry(context, client, state, job.JobId, environment, checkDelay, waitInterval, token);
                }
            }
            catch (BackgroundJobNotFoundException notFoundEx) when (maxCommitDate > DateTime.Now)
            {
                context.Log(LogLevel.Warning, $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Dequeued job will be delayed to <{maxCommitDate}>", notFoundEx, job.JobId, environment);
                await job.DelayToAsync(maxCommitDate.ToUniversalTime(), token).ConfigureAwait(false);
                return null;
            }
            catch (BackgroundJobNotFoundException notFoundEx)
            {
                context.Log(LogLevel.Error, $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Dequeued job will be dropped", notFoundEx, job.JobId, environment);
                await job.CompleteAsync(token).ConfigureAwait(false);
                return null;
            }
        }
        private async Task<IReadOnlyBackgroundJob> FetchBackgroundJobWithRetry(IDaemonExecutionContext context, IBackgroundJobClient client, IDroneState<WorkerSwarmHostOptions> state, string jobId, string environment, TimeSpan retryDelay, int maxCheck, CancellationToken token, int currentCheck = 0)
        {
            context.ValidateArgument(nameof(context));
            client.ValidateArgument(nameof(client));
            state.ValidateArgument(nameof(state));
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            maxCheck.ValidateArgumentLargerOrEqual(nameof(maxCheck), 1);
            currentCheck.ValidateArgumentLargerOrEqual(nameof(currentCheck), 0);

            context.Log(LogLevel.Debug, $"Trying to fetch background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> in <{retryDelay}>", jobId, environment);
            await Task.Delay(retryDelay).ConfigureAwait(false);

            try
            {
                return await client.GetAndTryLockAsync(context.Daemon.Colony.Environment, jobId, GetLockRequester(state), token).ConfigureAwait(false);
            }
            catch (BackgroundJobNotFoundException ex)
            {
                currentCheck++;

                var message = $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> ({currentCheck}/{maxCheck})";

                if (currentCheck < maxCheck)
                {
                    context.Log(LogLevel.Warning, message, ex, jobId, environment);
                    return await FetchBackgroundJobWithRetry(context, client, state, jobId, environment, retryDelay, maxCheck, token, currentCheck);
                }

                throw;
            }
        }
        private string GetLockRequester(IDroneState<WorkerSwarmHostOptions> state)
        {
            state.ValidateArgument(nameof(state));

            return $"WorkerDrone.{state.FullName}";
        }

        private bool CanBeProcessed(IDaemonExecutionContext context, IDequeuedJob job, IReadOnlyBackgroundJob backgroundJob)
        {
            context.ValidateArgument(nameof(context));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));

            // Execution matches
            if (!job.ExecutionId.Equals(backgroundJob.ExecutionId))
            {
                context.Log(LogLevel.Warning, $"Execution id of dequeued job does not match execution id of background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>. Can't process", backgroundJob.Id, backgroundJob.Environment);
                return false;
            }
            else if (!(backgroundJob.State is EnqueuedState))
            {
                context.Log(LogLevel.Warning, $"State of background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not <{nameof(EnqueuedState)}> but <{backgroundJob.State}>. Can't process", backgroundJob.Id, backgroundJob.Environment);
                return false;
            }

            return true;
        }

        private IDelayedPendingTask<IManagedTask> StartKeepAliveTask(ITaskManager taskManager, IDaemonExecutionContext context, IDroneState<WorkerSwarmHostOptions> state, IOptionsMonitor<HiveMindOptions> hiveOptions, CancellationTokenSource cancellationTokenSource, ILockedBackgroundJob backgroundJob, CancellationToken token)
        {
            taskManager.ValidateArgument(nameof(taskManager));
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            cancellationTokenSource.ValidateArgument(nameof(cancellationTokenSource));
            backgroundJob.ValidateArgument(nameof(backgroundJob));

            var setTime = backgroundJob.Lock.LockHeartbeat.Add(hiveOptions.CurrentValue.LockTimeout).Add(-(state.Swarm.Options.LockHeartbeatSafetyOffset ?? _defaultWorkerOptions.CurrentValue.LockHeartbeatSafetyOffset));
            return taskManager.ScheduleDelayed(setTime, (m, t) =>
            {
                return m.ScheduleActionAsync(this, "KeepAliveTask", false, async t =>
                {
                    context.Log(LogLevel.Debug, $"Keep alive task for background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> started", backgroundJob.Id, state.FullName);

                    while (!t.IsCancellationRequested)
                    {
                        var setTime = backgroundJob.Lock.LockHeartbeat.Add(hiveOptions.CurrentValue.LockTimeout).Add(-(state.Swarm.Options.LockHeartbeatSafetyOffset ?? _defaultWorkerOptions.CurrentValue.LockHeartbeatSafetyOffset));
                        context.Log(LogLevel.Debug, $"Keeping lock on background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> alive at <{setTime}>", backgroundJob.Id, state.FullName);
                        await Helper.Async.SleepUntil(setTime, t).ConfigureAwait(false);
                        if (t.IsCancellationRequested) return;

                        try
                        {
                            context.Log(LogLevel.Debug, $"Lock on dequeued job background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> is about to expire. Trying to heartbeat to see if we still have lock", backgroundJob.Id, state.FullName);
                            if (!await backgroundJob.SetHeartbeatAsync(t).ConfigureAwait(false))
                            {
                                context.Log(LogLevel.Warning, $"Lock on dequeued job background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> expired. Cancelling", backgroundJob.Id, state.FullName);
                                cancellationTokenSource.Cancel();
                                break;
                            }
                            else
                            {
                                context.Log(LogLevel.Debug, $"Kept lock on background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> alive", backgroundJob.Id, state.FullName);
                            }
                        }
                        catch (OperationCanceledException) when (t.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            context.Log(LogLevel.Error, $"Could not keep lock on background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> alive. Cancelling", ex, backgroundJob.Id, state.FullName);
                            cancellationTokenSource.Cancel();
                            break;
                        }
                    }
                    context.Log(LogLevel.Debug, $"Keep alive task for background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> stopped", backgroundJob.Id, state.FullName);
                }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation)
                         .WithPolicy(NamedManagedTaskPolicy.CancelAndStart)
                         .WithCreationOptions(TaskCreationOptions.PreferFairness)
                , token);
            });
        }

        private IEnumerable<IMiddlewareInfo> GetMiddleware(IDaemonExecutionContext context, HiveMindOptions hiveOptions, IDroneState<WorkerSwarmHostOptions> state, IReadOnlyBackgroundJob job, IMemoryCache cache = null)
        {
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));

            if (job.Middleware.HasValue())
            {
                foreach(var middleware in job.Middleware)
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
            while(useFromParentSwarm && currentSwarm != null);
        }
        private async Task<(IBackgroundJobMiddleware Middleware, IMiddlewareInfo Info)[]> ActiveMiddleware(IActivatorScope activatorScope, IEnumerable<IMiddlewareInfo> middleware)
        {
            activatorScope.ValidateArgument(nameof(activatorScope));
            middleware.ValidateArgument(nameof(middleware));
            var jobMiddleware = new List<(IBackgroundJobMiddleware Middleware, IMiddlewareInfo Info)>();

            foreach(var info in middleware)
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
                foreach(var methodParameter in invocationInfo.MethodInfo.GetParameters())
                {
                    // Access method argument from parameters
                    var arrayAccessExpression = LinqExpression.ArrayAccess(argumentParameter, LinqExpression.Constant(methodParameter.Position));
                    // Cast array element to method parameter type
                    methodParameters.Add(LinqExpression.Convert(arrayAccessExpression, methodParameter.ParameterType));

                    if(methodParameter.ParameterType.Is<IBackgroundJobExecutionContext>()) jobContextIndex = methodParameter.Position;
                    else if (methodParameter.ParameterType.Is<CancellationToken>()) cancellationTokenIndex = methodParameter.Position;
                }
                LinqExpression methodCall = instanceVariable != null ? LinqExpression.Call(instanceVariable, invocationInfo.MethodInfo, methodParameters) : LinqExpression.Call(invocationInfo.MethodInfo, methodParameters);
                if(invocationInfo.MethodInfo.ReturnType != null && invocationInfo.MethodInfo.ReturnType.IsValueType)
                {
                    // Need explicit casting for value types
                    methodCall = LinqExpression.Convert(methodCall, typeof(object));
                }
                bodyExpressions.Add(methodCall);

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
                if(currentMiddleware < jobMiddleware.Length)
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
                        using(Helper.Time.CaptureDuration(x => c.Duration = x))
                        {
                            var result = invocationDelegate(c.JobInstance, c.InvocationArguments);

                            // Await result if async
                            if(result is ValueTask valueTask)
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
                    catch(Exception ex) 
                    {
                        context.Log(LogLevel.Warning, $"Drone <{state.FullName}> received exception from invoking background job <{HiveLog.Job.Id}>", ex, jobExecutionContext.Job.Id);
                        c.Result = ex;
                    }
                }
            });

            await next(jobExecutionContext, token);
        }
    }
}
