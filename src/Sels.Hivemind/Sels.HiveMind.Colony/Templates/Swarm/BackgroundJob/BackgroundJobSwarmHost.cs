using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Text;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker;
using Sels.HiveMind.Colony.Swarm.BackgroundJob;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.ObjectValidationFramework.Extensions.Validation;
using Sels.ObjectValidationFramework.Validators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Validation;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Interval;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob
{
    /// <summary>
    /// Base class for creating a swarm that processes background jobs placed in queues of a supplied queue type.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by this host</typeparam>
    /// <typeparam name="TDefaultOptions">The type of default options used by this host</typeparam>
    public abstract class BackgroundJobSwarmHost<TOptions, TDefaultOptions> : SwarmHost<TOptions, TDefaultOptions>
        where TOptions : IBackgroundJobSwarmHostOptions<TOptions>
        where TDefaultOptions : BackgroundJobSwarmHostDefaultOptions
    {
        // Fields

        /// <inheritdoc cref="BackgroundJobSwarmHost{TOptions, TDefaultOptions}"/>
        /// <param name="queueType"><inheritdoc cref="SwarmHost{TOptions, TDefaultOptions}.Options"/></param>
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
        protected BackgroundJobSwarmHost(string queueType, IOptionsMonitor<TDefaultOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null) : base(queueType, defaultOptions, jobQueueProvider, schedulerProvider, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
        }

        /// <inheritdoc/>
        protected override async Task ProcessAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IServiceProvider serviceProvider, IDequeuedJob job, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            var client = serviceProvider.GetRequiredService<IBackgroundJobClient>();
            var environment = context.Daemon.Colony.Environment;
            var hiveOptions = serviceProvider.GetRequiredService<IOptionsMonitor<HiveMindOptions>>();
            context.Log($"Drone <{HiveLog.Swarm.DroneName}> received background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> to process", state.FullName, job.JobId, environment);

            // Fetch job
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneName}> fetching background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for reading", state.FullName, job.JobId, environment);
            IReadOnlyBackgroundJob readOnlyBackgroundJob = await FetchBackgroundJob(context, client, state, job, token).ConfigureAwait(false);
            if (readOnlyBackgroundJob == null) return;
            await using var readOnlyBackgroundJobScope = readOnlyBackgroundJob;
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneName}> fetched background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for reading. Checking state", state.FullName, job.JobId, environment);

            // Check if job can be processed
            if (!CanBeProcessed(context, state, job, readOnlyBackgroundJob, hiveOptions.Get(environment), out var delay))
            {
                if (delay.HasValue)
                {
                    var delayToDate = DateTime.UtcNow.Add(delay.Value);
                    context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be processed. Job will be delayed to <{delayToDate}>", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                    await job.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                }
                else
                {
                    context.Log(LogLevel.Error, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be processed. Dropping dequeued job", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                    await job.CompleteAsync(token).ConfigureAwait(false);
                }
                return;
            }

            // Check if job can be locked
            context.Log(LogLevel.Debug, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> can be processed by drone <{state.FullName}>. Checking if it can be locked", job.JobId, environment);
            if (readOnlyBackgroundJob.IsLocked && !readOnlyBackgroundJob.Lock.LockedBy.EqualsNoCase(GetLockRequester(state)))
            {
                var delayToDate = DateTime.UtcNow.Add(state.Swarm.Options.LockedDelay ?? _defaultOptions.CurrentValue.LockedDelay);
                context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is already locked. Dequeued job will be delayed to <{delayToDate}>", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                await job.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                return;
            }

            // Lock job and check again just to be sure
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneName}> attempting to lock background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", state.FullName, job.JobId, environment);
            await using var backgroundJob = await readOnlyBackgroundJob.LockAsync(GetLockRequester(state), token).ConfigureAwait(false);
            if (!CanBeProcessed(context, state, job, backgroundJob, hiveOptions.Get(environment), out delay))
            {
                if (delay.HasValue)
                {
                    var delayToDate = DateTime.UtcNow.Add(delay.Value);
                    context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be processed after acquiring lock. Job will be delayed to <{delayToDate}>", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                    await job.DelayToAsync(delayToDate, token).ConfigureAwait(false);
                }
                else
                {
                    context.Log(LogLevel.Error, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be processed after acquiring lock. Dropping dequeued job", readOnlyBackgroundJob.Id, readOnlyBackgroundJob.Environment);
                    await job.CompleteAsync(token).ConfigureAwait(false);
                }
                return;
            }
            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneName}> got lock on background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>. Starting keep alive task for lock", state.FullName, job.JobId, environment);

            // Start keep alive task to keep lock on background job alive
            var taskManager = serviceProvider.GetRequiredService<ITaskManager>();
            var jobTokenSource = new CancellationTokenSource();
            var forceStopSource = new CancellationTokenSource();
            using var tokenRegistration = token.Register(() =>
            {
                jobTokenSource.Cancel();
                forceStopSource.CancelAfter(state.Swarm.Options.MaxSaveTime ?? _defaultOptions.CurrentValue.GracefulStoptime);
            });
            var keepAliveTask = StartKeepAliveTask(taskManager, context, state, hiveOptions, jobTokenSource, backgroundJob, token);

            try
            {
                context.Log($"Drone <{HiveLog.Swarm.DroneName}> started processing background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", state.FullName, job.JobId, environment);
                await ProcessJobAsync(context, state, job, backgroundJob, hiveOptions.Get(environment), jobTokenSource, forceStopSource.Token).ConfigureAwait(false);
                context.Log($"Drone <{HiveLog.Swarm.DroneName}> processed background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", state.FullName, job.JobId, environment);
            }
            catch (OperationCanceledException cancelledEx) when (token.IsCancellationRequested)
            {
                context.Log(LogLevel.Warning, $"Drone <{HiveLog.Swarm.DroneName}> was cancelled while processing background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", cancelledEx, state.FullName, job.JobId, environment);
            }
            catch (Exception ex)
            {
                context.Log(LogLevel.Error, $"Drone <{HiveLog.Swarm.DroneName}> could not process background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>", ex, state.FullName, job.JobId, environment);
                await HandleErrorAsync(context, state, job, backgroundJob, hiveOptions.Get(environment), ex, forceStopSource.Token);
            }
            finally
            {
                await keepAliveTask.CancelAndWaitOnFinalization(forceStopSource.Token).ConfigureAwait(false);
            }
        }

        private async Task<IReadOnlyBackgroundJob> FetchBackgroundJob(IDaemonExecutionContext context, IBackgroundJobClient client, IDroneState<TOptions> state, IDequeuedJob job, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            client.ValidateArgument(nameof(client));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));

            var environment = context.Daemon.Colony.Environment;
            var maxCommitTime = state.Swarm.Options.MaxJobCommitTime ?? _defaultOptions.CurrentValue.MaxJobCommitTime;
            var maxCommitDate = job.EnqueuedAt.Add(maxCommitTime);
            try
            {
                try
                {
                    return await client.GetAndTryLockAsync(context.Daemon.Colony.Environment, job.JobId, GetLockRequester(state), token).ConfigureAwait(false);
                }
                catch (JobNotFoundException) when (maxCommitDate > DateTime.Now)
                {
                    var maxWaitTime = state.Swarm.Options.MaxNotFoundWaitTime ?? _defaultOptions.CurrentValue.MaxNotFoundWaitTime;
                    var waitInterval = state.Swarm.Options.NotFoundCheckInterval ?? _defaultOptions.CurrentValue.NotFoundCheckInterval;
                    var checkDelay = maxWaitTime / waitInterval;
                    context.Log(LogLevel.Warning, $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> but dequeued job is still within the commit window of <{maxCommitTime}>. Rechecking every <{checkDelay}> for a maximum of <{waitInterval}> times", job.JobId, environment);
                    return await FetchBackgroundJobWithRetry(context, client, state, job.JobId, environment, checkDelay, waitInterval, token);
                }
            }
            catch (JobNotFoundException notFoundEx) when (maxCommitDate > DateTime.Now)
            {
                context.Log(LogLevel.Warning, $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Dequeued job will be delayed to <{maxCommitDate}>", notFoundEx, job.JobId, environment);
                await job.DelayToAsync(maxCommitDate.ToUniversalTime(), token).ConfigureAwait(false);
                return null;
            }
            catch (JobNotFoundException notFoundEx)
            {
                context.Log(LogLevel.Error, $"Could not find background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Dequeued job will be dropped", notFoundEx, job.JobId, environment);
                await job.CompleteAsync(token).ConfigureAwait(false);
                return null;
            }
        }
        private async Task<IReadOnlyBackgroundJob> FetchBackgroundJobWithRetry(IDaemonExecutionContext context, IBackgroundJobClient client, IDroneState<TOptions> state, string jobId, string environment, TimeSpan retryDelay, int maxCheck, CancellationToken token, int currentCheck = 0)
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
            catch (JobNotFoundException ex)
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
        private string GetLockRequester(IDroneState<TOptions> state)
        {
            state.ValidateArgument(nameof(state));

            return $"WorkerDrone.{state.FullName}";
        }

        private IDelayedPendingTask<IManagedTask> StartKeepAliveTask(ITaskManager taskManager, IDaemonExecutionContext context, IDroneState<TOptions> state, IOptionsMonitor<HiveMindOptions> hiveOptions, CancellationTokenSource cancellationTokenSource, ILockedBackgroundJob backgroundJob, CancellationToken token)
        {
            taskManager.ValidateArgument(nameof(taskManager));
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            hiveOptions.ValidateArgument(nameof(hiveOptions));
            cancellationTokenSource.ValidateArgument(nameof(cancellationTokenSource));
            backgroundJob.ValidateArgument(nameof(backgroundJob));

            var currentOptions = hiveOptions.Get(context.Daemon.Colony.Environment);
            var setTime = backgroundJob.Lock.LockHeartbeat.Add(currentOptions.LockTimeout).Add(-currentOptions.LockExpirySafetyOffset);
            return taskManager.ScheduleDelayed(setTime, (m, t) =>
            {
                return m.ScheduleActionAsync(this, "KeepAliveTask", false, async t =>
                {
                    context.Log(LogLevel.Information, $"Keep alive task for background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> started", backgroundJob.Id, state.FullName);

                    while (!t.IsCancellationRequested)
                    {
                        currentOptions = hiveOptions.Get(context.Daemon.Colony.Environment);
                        var setTime = backgroundJob.Lock?.LockHeartbeat.Add(currentOptions.LockTimeout).Add(-currentOptions.LockExpirySafetyOffset);
                        if (!setTime.HasValue) return;
                        context.Log(LogLevel.Debug, $"Keeping lock on background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> alive at <{setTime}>", backgroundJob.Id, state.FullName);
                        await Helper.Async.SleepUntil(setTime.Value, t).ConfigureAwait(false);
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
                                context.Log(LogLevel.Information, $"Kept lock on background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> alive", backgroundJob.Id, state.FullName);
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
                    context.Log(LogLevel.Information, $"Keep alive task for background job <{HiveLog.Job.Id}> for Drone <{state.FullName}> stopped", backgroundJob.Id, state.FullName);
                }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation)
                         .WithPolicy(NamedManagedTaskPolicy.CancelAndStart)
                         .WithCreationOptions(TaskCreationOptions.PreferFairness)
                , token);
            });
        }


        /// <summary>
        /// Checks if <paramref name="backgroundJob"/> is in a valid state to be processed.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="job">The dequeued job</param>
        /// <param name="backgroundJob">The background job to process</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <returns>True if <paramref name="backgroundJob"/> is in a state that can be processed or false if <paramref name="backgroundJob"/> doesn't need to be processed. When false and <paramref name="delay"/> is set <paramref name="job"/> will be delayed, otherwise completed</returns>
        protected abstract bool CanBeProcessed(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob job, IReadOnlyBackgroundJob backgroundJob, HiveMindOptions options, out TimeSpan? delay);

        /// <summary>
        /// Processes <paramref name="backgroundJob"/>.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="job">The dequeued job</param>
        /// <param name="backgroundJob">The background job to process</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <param name="jobTokenSource">The token source that is used to cancel the job</param>
        /// <param name="token">Token that will be cancelled when the drone is requested to stop processing</param>
        /// <returns>Task that should complete when <paramref name="backgroundJob"/> was processed</returns>
        protected abstract Task ProcessJobAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, CancellationTokenSource jobTokenSource, CancellationToken token);

        /// <summary>
        /// Handles uncaught exception that was thrown during the processing of <paramref name="backgroundJob"/>.
        /// </summary>
        /// <param name="context">The context of the daemon that is currently processing the job</param>
        /// <param name="state">The current state of the drone that is processing the job</param>
        /// <param name="job">The dequeued job</param>
        /// <param name="backgroundJob">The background job that could not be processed</param>
        /// <param name="options">The configured options for the current environment</param>
        /// <param name="exception">The exception that was thrown during processing</param>
        /// <param name="token">Token that will be cancelled when the drone is requested to stop processing</param>
        /// <returns>Task that should complete when the exception was handled</returns>
        protected abstract Task HandleErrorAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, Exception exception, CancellationToken token);
    }
}
