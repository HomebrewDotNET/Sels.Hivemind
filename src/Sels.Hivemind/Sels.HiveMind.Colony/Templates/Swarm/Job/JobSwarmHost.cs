using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Validation;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// Base class for creating a swarm that processes <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}"/> placed in queues of a supplied queue type.
    /// </summary>
    /// <typeparam name="TReadOnlyJob">The type of the readonly version of the jobs to process</typeparam>
    /// <typeparam name="TLockedJob">The type of the locked version of the jobs to process</typeparam>
    /// <typeparam name="TSortTarget">The type used to determine the sort order when querying the jobs</typeparam>
    /// <typeparam name="TOptions">The type of options used by this host</typeparam>
    /// <typeparam name="TDefaultOptions">The type of default options used by this host</typeparam>
    /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
    public abstract class JobSwarmHost<TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions> : SwarmHost<TOptions, TDefaultOptions>
        where TOptions : IJobSwarmHostOptions<TOptions>
        where TDefaultOptions : JobSwarmHostDefaultOptions
        where TReadOnlyJob : class, IReadOnlyJob<TLockedJob, TChangeTracker, TState, TAction>
        where TLockedJob : ILockedJob<TLockedJob, TChangeTracker, TState, TAction>
        where TState : IJobState
        where TChangeTracker : IJobChangeTracker<TState>
    {
        // Fields
        /// <summary>
        /// The client used to fetch the jobs to process.
        /// </summary>
        protected readonly IJobClient<TReadOnlyJob, TLockedJob, TSortTarget> _client;

        /// <inheritdoc cref="JobSwarmHost{TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions}"/>
        /// <param name="client"><inheritdoc cref="_client"/></param>
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
        protected JobSwarmHost(IJobClient<TReadOnlyJob, TLockedJob, TSortTarget> client, string queueType, IOptionsMonitor<TDefaultOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
        : base(queueType, defaultOptions, jobQueueProvider, schedulerProvider, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _client = Guard.IsNotNull(client);
        }

        /// <inheritdoc/>
        protected override async Task ProcessAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IServiceProvider serviceProvider, IDequeuedJob dequeuedJob, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            dequeuedJob.ValidateArgument(nameof(dequeuedJob));
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            var environment = context.Daemon.Colony.Environment;
            var hiveOptions = serviceProvider.GetRequiredService<IOptionsMonitor<HiveMindOptions>>();
            context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> received job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> to process", state.FullName, dequeuedJob.JobId, environment);

            // Fetch job
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneNameParam}> fetching job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> for reading", state.FullName, dequeuedJob.JobId, environment);
            TReadOnlyJob? readOnlyJob = await FetchJob(context,  state, dequeuedJob, token).ConfigureAwait(false);
            if (readOnlyJob == null) return;
            await using var readOnlyJobScope = readOnlyJob;
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneNameParam}> fetched job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> for reading. Checking state", state.FullName, dequeuedJob.JobId, environment);

            // Check if job can be processed
            if (!CanBeProcessed(context, state, dequeuedJob, readOnlyJob, hiveOptions.Get(environment), out var delay))
            {
                if (delay.HasValue)
                {
                    var delayToDate = DateTime.UtcNow.Add(delay.Value);
                    context.Log(LogLevel.Warning, $"Job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not in a valid state to be processed. Job will be delayed to <{delayToDate}>", readOnlyJob.Id, readOnlyJob.Environment);
                    await dequeuedJob.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                }
                else
                {
                    context.Log(LogLevel.Error, $"Job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not in a valid state to be processed. Dropping dequeued job", readOnlyJob.Id, readOnlyJob.Environment);
                    await dequeuedJob.CompleteAsync(token).ConfigureAwait(false);
                }
                return;
            }

            // Check if job can be locked
            context.Log(LogLevel.Debug, $"Job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> can be processed by drone <{state.FullName}>. Checking if it can be locked", dequeuedJob.JobId, environment);
            if (!readOnlyJob.HasLock)
            {
                var delayToDate = DateTime.UtcNow.Add(state.Swarm.Options.LockedDelay ?? _defaultOptions.CurrentValue.LockedDelay);
                context.Log(LogLevel.Warning, $"Job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is already locked. Dequeued job will be delayed to <{delayToDate}>", readOnlyJob.Id, readOnlyJob.Environment);
                await dequeuedJob.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                return;
            }

            // Lock job and check again just to be sure
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneNameParam}> attempting to lock job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", state.FullName, dequeuedJob.JobId, environment);
            await using var lockedJob = await readOnlyJob.LockAsync(GetLockRequester(state), token).ConfigureAwait(false);
            if (!CanBeProcessed(context, state, dequeuedJob, lockedJob, hiveOptions.Get(environment), out delay))
            {
                if (delay.HasValue)
                {
                    var delayToDate = DateTime.UtcNow.Add(delay.Value);
                    context.Log(LogLevel.Warning, $"Job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not in a valid state to be processed after acquiring lock. Job will be delayed to <{delayToDate}>", readOnlyJob.Id, readOnlyJob.Environment);
                    await dequeuedJob.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                }
                else
                {
                    context.Log(LogLevel.Error, $"Job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not in a valid state to be processed after acquiring lock. Dropping dequeued job", readOnlyJob.Id, readOnlyJob.Environment);
                    await dequeuedJob.CompleteAsync(token).ConfigureAwait(false);
                }
                return;
            }
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneNameParam}> got lock on job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>. Starting keep alive task for lock", state.FullName, dequeuedJob.JobId, environment);

            // Start keep alive task to keep lock on job alive
            var taskManager = serviceProvider.GetRequiredService<ITaskManager>();
            var jobTokenSource = new CancellationTokenSource();
            var forceStopSource = new CancellationTokenSource();
            using var tokenRegistration = token.Register(() =>
            {
                jobTokenSource.Cancel();
                forceStopSource.CancelAfter(state.Swarm.Options.MaxSaveTime ?? _defaultOptions.CurrentValue.GracefulStoptime);
            });
            var keepAliveTask = StartKeepAliveTask(taskManager, context, state, hiveOptions, jobTokenSource, lockedJob, token);

            try
            {
                context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> started processing job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", state.FullName, dequeuedJob.JobId, environment);
                await ProcessJobAsync(context, state, dequeuedJob, lockedJob, hiveOptions.Get(environment), jobTokenSource, forceStopSource.Token).ConfigureAwait(false);
                context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> processed job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", state.FullName, dequeuedJob.JobId, environment);
            }
            catch (OperationCanceledException cancelledEx) when (token.IsCancellationRequested)
            {
                context.Log(LogLevel.Warning, $"Drone <{HiveLog.Swarm.DroneNameParam}> was cancelled while processing job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", cancelledEx, state.FullName, dequeuedJob.JobId, environment);
            }
            catch (Exception ex)
            {
                context.Log(LogLevel.Error, $"Drone <{HiveLog.Swarm.DroneNameParam}> could not process job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", ex, state.FullName, dequeuedJob.JobId, environment);
                await HandleErrorAsync(context, state, dequeuedJob, lockedJob, hiveOptions.Get(environment), ex, forceStopSource.Token);
            }
            finally
            {
                await keepAliveTask.CancelAndWaitOnFinalization(forceStopSource.Token).ConfigureAwait(false);
            }
        }

        private async Task<TReadOnlyJob?> FetchJob(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob job, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));

            var environment = context.Daemon.Colony.Environment;
            var maxCommitTime = state.Swarm.Options.MaxJobCommitTime ?? _defaultOptions.CurrentValue.MaxJobCommitTime;
            var maxCommitDate = job.EnqueuedAt.Add(maxCommitTime);
            try
            {
                try
                {
                    return await _client.GetAndTryLockAsync(context.Daemon.Colony.Environment, job.JobId, GetLockRequester(state), token).ConfigureAwait(false);
                }
                catch (JobNotFoundException) when (maxCommitDate > DateTime.Now)
                {
                    var maxWaitTime = state.Swarm.Options.MaxNotFoundWaitTime ?? _defaultOptions.CurrentValue.MaxNotFoundWaitTime;
                    var waitInterval = state.Swarm.Options.NotFoundCheckInterval ?? _defaultOptions.CurrentValue.NotFoundCheckInterval;
                    var checkDelay = maxWaitTime / waitInterval;
                    context.Log(LogLevel.Warning, $"Could not find job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> but dequeued job is still within the commit window of <{maxCommitTime}>. Rechecking every <{checkDelay}> for a maximum of <{waitInterval}> times", job.JobId, environment);
                    return await FetchJobWithRetry(context, state, job.JobId, environment, checkDelay, waitInterval, token);
                }
            }
            catch (JobNotFoundException notFoundEx) when (maxCommitDate > DateTime.Now)
            {
                context.Log(LogLevel.Warning, $"Could not find job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Dequeued job will be delayed to <{maxCommitDate}>", notFoundEx, job.JobId, environment);
                await job.DelayToAsync(maxCommitDate.ToUniversalTime(), token).ConfigureAwait(false);
                return null;
            }
            catch (JobNotFoundException notFoundEx)
            {
                context.Log(LogLevel.Error, $"Could not find job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Dequeued job will be dropped", notFoundEx, job.JobId, environment);
                await job.CompleteAsync(token).ConfigureAwait(false);
                return null;
            }
        }
        private async Task<TReadOnlyJob?> FetchJobWithRetry(IDaemonExecutionContext context, IDroneState<TOptions> state, string jobId, string environment, TimeSpan retryDelay, int maxCheck, CancellationToken token, int currentCheck = 0)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            maxCheck.ValidateArgumentLargerOrEqual(nameof(maxCheck), 1);
            currentCheck.ValidateArgumentLargerOrEqual(nameof(currentCheck), 0);

            context.Log(LogLevel.Debug, $"Trying to fetch job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> in <{retryDelay}>", jobId, environment);
            await Task.Delay(retryDelay).ConfigureAwait(false);

            try
            {
                return await _client.GetAndTryLockAsync(context.Daemon.Colony.Environment, jobId, GetLockRequester(state), token).ConfigureAwait(false);
            }
            catch (JobNotFoundException ex)
            {
                currentCheck++;

                var message = $"Could not find job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> ({currentCheck}/{maxCheck})";

                if (currentCheck < maxCheck)
                {
                    context.Log(LogLevel.Warning, message, ex, jobId, environment);
                    return await FetchJobWithRetry(context, state, jobId, environment, retryDelay, maxCheck, token, currentCheck);
                }

                throw;
            }
        }
        private string GetLockRequester(IDroneState<TOptions> state)
        {
            state.ValidateArgument(nameof(state));

            return $"{GetType().Name}.{state.FullName}";
        }

        private IDelayedPendingTask<IManagedTask> StartKeepAliveTask(ITaskManager taskManager, IDaemonExecutionContext context, IDroneState<TOptions> state, IOptionsMonitor<HiveMindOptions> hiveOptions, CancellationTokenSource cancellationTokenSource, TLockedJob job, CancellationToken token)
        {
            taskManager.ValidateArgument(nameof(taskManager));
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            cancellationTokenSource.ValidateArgument(nameof(cancellationTokenSource));
            job.ValidateArgument(nameof(job));

            var currentOptions = hiveOptions.Get(context.Daemon.Colony.Environment);
            var setTime = job.Lock.LockHeartbeat.Add(currentOptions.LockTimeout).Add(-currentOptions.LockExpirySafetyOffset);
            return taskManager.ScheduleDelayed(setTime, (m, t) =>
            {
                return m.ScheduleActionAsync(this, "KeepAliveTask", false, async t =>
                {
                    context.Log(LogLevel.Information, $"Keep alive task for job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> started", job.Id, state.FullName);

                    while (!t.IsCancellationRequested)
                    {
                        currentOptions = hiveOptions.Get(context.Daemon.Colony.Environment);
                        var setTime = job.Lock?.LockHeartbeat.Add(currentOptions.LockTimeout).Add(-currentOptions.LockExpirySafetyOffset);
                        if (!setTime.HasValue) return;
                        context.Log(LogLevel.Debug, $"Keeping lock on job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> alive at <{setTime}>", job.Id, state.FullName);
                        await Helper.Async.SleepUntil(setTime.Value, t).ConfigureAwait(false);
                        if (t.IsCancellationRequested) return;

                        try
                        {
                            context.Log(LogLevel.Debug, $"Lock on dequeued job job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> is about to expire. Trying to heartbeat to see if we still have lock", job.Id, state.FullName);
                            if (!await job.SetHeartbeatAsync(t).ConfigureAwait(false))
                            {
                                context.Log(LogLevel.Warning, $"Lock on dequeued job job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> expired. Cancelling", job.Id, state.FullName);
                                cancellationTokenSource.Cancel();
                                break;
                            }
                            else
                            {
                                context.Log(LogLevel.Information, $"Kept lock on job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> alive", job.Id, state.FullName);
                            }
                        }
                        catch (OperationCanceledException) when (t.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            context.Log(LogLevel.Error, $"Could not keep lock on job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> alive. Cancelling", ex, job.Id, state.FullName);
                            cancellationTokenSource.Cancel();
                            break;
                        }
                    }
                    context.Log(LogLevel.Information, $"Keep alive task for job <{HiveLog.Job.IdParam}> for Drone <{state.FullName}> stopped", job.Id, state.FullName);
                }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation)
                         .WithPolicy(NamedManagedTaskPolicy.CancelAndStart)
                         .WithCreationOptions(TaskCreationOptions.PreferFairness)
                , token);
            });
        }


        /// <summary>
        /// Checks if <paramref name="job"/> is in a valid state to be processed.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="dequeudJob">The dequeued job</param>
        /// <param name="job">The background job to process</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <returns>True if <paramref name="job"/> is in a state that can be processed or false if <paramref name="job"/> doesn't need to be processed. When false and <paramref name="delay"/> is set <paramref name="dequeudJob"/> will be delayed, otherwise completed</returns>
        protected abstract bool CanBeProcessed(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob dequeudJob, TReadOnlyJob job, HiveMindOptions options, out TimeSpan? delay);

        /// <summary>
        /// Checks if <paramref name="job"/> is in a valid state to be processed.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="dequeudJob">The dequeued job</param>
        /// <param name="job">The background job to process</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <returns>True if <paramref name="job"/> is in a state that can be processed or false if <paramref name="job"/> doesn't need to be processed. When false and <paramref name="delay"/> is set <paramref name="dequeudJob"/> will be delayed, otherwise completed</returns>
        protected abstract bool CanBeProcessed(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob dequeudJob, TLockedJob job, HiveMindOptions options, out TimeSpan? delay);

        /// <summary>
        /// Processes <paramref name="job"/>.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="dequeuedJob">The dequeued job</param>
        /// <param name="job">The job to process</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <param name="jobTokenSource">The token source that is used to cancel the job</param>
        /// <param name="token">Token that will be cancelled when the drone is requested to stop processing</param>
        /// <returns>Task that should complete when <paramref name="job"/> was processed</returns>
        protected abstract Task ProcessJobAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob dequeuedJob, TLockedJob job, HiveMindOptions options, CancellationTokenSource jobTokenSource, CancellationToken token);

        /// <summary>
        /// Handles uncaught exception that was thrown during the processing of <paramref name="job"/>.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="dequeuedJob">The dequeued job</param>
        /// <param name="job">The background job that could not be processed</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <param name="exception">The exception that was thrown during processing</param>
        /// <param name="token">Token that will be cancelled when the drone is requested to stop processing</param>
        /// <returns>Task that should complete when the exception was handled</returns>
        protected abstract Task HandleErrorAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob dequeuedJob, TLockedJob job, HiveMindOptions options, Exception exception, CancellationToken token);
    }
}
