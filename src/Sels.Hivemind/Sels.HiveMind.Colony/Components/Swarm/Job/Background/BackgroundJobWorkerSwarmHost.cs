using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.HiveMind.HiveLog;

namespace Sels.HiveMind.Colony.Swarm.Job.Background
{
    /// <summary>
    /// A swarm that executes pending background jobs.
    /// </summary>
    public class BackgroundJobWorkerSwarmHost : WorkerSwarmHost<BackgroundJobWorkerSwarmHostOptions, IBackgroundJobExecutionContext, IBackgroundJobMiddleware, IWriteableBackgroundJob, IReadOnlyBackgroundJob, ILockedBackgroundJob, IBackgroundJobChangeTracker, IBackgroundJobState, IBackgroundJobAction, QueryBackgroundJobOrderByTarget?, IBackgroundJobWorkerSwarmHostOptions, BackgroundJobWorkerSwarmDefaultHostOptions>
    {
        // Fields
        /// <inheritdoc cref="JobSwarmHost{TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions}._client"/>
        protected new readonly IBackgroundJobClient _client;

        /// <inheritdoc cref="BackgroundJobWorkerSwarmHost"/>
        /// <param name="initialOptions">The initial options used by the current swarm</param>
        /// <param name="client"><inheritdoc cref="_client"></inheritdoc></param>
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
        public BackgroundJobWorkerSwarmHost(BackgroundJobWorkerSwarmHostOptions initialOptions, IBackgroundJobClient client, string queueType, IOptionsMonitor<BackgroundJobWorkerSwarmDefaultHostOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
        : base(initialOptions, client, queueType, defaultOptions, jobQueueProvider, schedulerProvider, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _client = Guard.IsNotNull(client);
        }

        /// <inheritdoc/>
        protected override async Task ProcessAsync(IDaemonExecutionContext context, IDroneState<IBackgroundJobWorkerSwarmHostOptions> state, IServiceProvider serviceProvider, IDequeuedJob dequeuedJob, CancellationToken token)
        {
            using var loggerScope = _logger.TryBeginScope(new Dictionary<string, object>
            {
                { HiveLog.Job.Type, HiveLog.Job.BackgroundJobType }
            });
            await base.ProcessAsync(context, state, serviceProvider, dequeuedJob, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override Task<(bool CanProcess, TimeSpan? Delay)> CanBeProcessedAsync(IDaemonExecutionContext context, IDroneState<IBackgroundJobWorkerSwarmHostOptions> state, IDequeuedJob job, IReadOnlyBackgroundJob backgroundJob, HiveMindOptions options, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));

            TimeSpan? delay = null;

            // Execution matches
            if (!job.ExecutionId.Equals(backgroundJob.ExecutionId))
            {
                context.Log(LogLevel.Warning, $"Execution id of dequeued job does not match execution id of background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>. Can't process", backgroundJob.Id, backgroundJob.Environment);
                return Task.FromResult<(bool CanProcess, TimeSpan? Delay)>((false, delay));
            }
            else if (!(backgroundJob.State is EnqueuedState))
            {
                context.Log(LogLevel.Warning, $"State of background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not <{nameof(EnqueuedState)}> but <{HiveLog.Job.StateParam}>. Can't process", backgroundJob.Id, backgroundJob.Environment, backgroundJob.State);
                return Task.FromResult<(bool CanProcess, TimeSpan? Delay)>((false, delay));
            }

            return Task.FromResult<(bool CanProcess, TimeSpan? Delay)>((true, delay));
        }
        /// <inheritdoc/>
        protected override Task<(bool CanProcess, TimeSpan? Delay)> CanBeProcessedAsync(IDaemonExecutionContext context, IDroneState<IBackgroundJobWorkerSwarmHostOptions> state, IDequeuedJob dequeudJob, ILockedBackgroundJob job, HiveMindOptions options, CancellationToken token)
        => CanBeProcessedAsync(context, state, dequeudJob, job.CastTo<IReadOnlyBackgroundJob>(), options, token);
        /// <inheritdoc/>
        protected override IBackgroundJobExecutionContext CreateContext(IDaemonExecutionContext context, IDroneState<IBackgroundJobWorkerSwarmHostOptions> state, IServiceProvider serviceProvider, ILockedBackgroundJob job, object? instance, object[] arguments, CancellationTokenSource tokenSource, LogLevel logLevel, TimeSpan logFlushInterval, TimeSpan actionPollingInterval, int actionFetchLimit, IActivatorScope activatorScope, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory)
        => new BackgroundJobExecutionContext(context, state, job, instance, arguments, tokenSource, logLevel, logFlushInterval, actionPollingInterval, actionFetchLimit, context.ServiceProvider.GetRequiredService<IBackgroundJobService>(), activatorScope, taskManager, storage, loggerFactory);
        /// <inheritdoc/>
        protected override async Task ExecuteJobAsync(IDaemonExecutionContext context, IDroneState<IBackgroundJobWorkerSwarmHostOptions> state, IServiceProvider serviceProvider, IBackgroundJobExecutionContext jobExecutionContext, IDequeuedJob dequeuedJob, ILockedBackgroundJob job, Func<Task> executeJob, Stopwatch stopwatch, CancellationTokenSource jobTokenSource, IActivatorScope activatorScope, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory, CancellationToken token)
        {
            context = Guard.IsNotNull(context);
            state = Guard.IsNotNull(state);
            jobExecutionContext = Guard.IsNotNull(jobExecutionContext);
            dequeuedJob = Guard.IsNotNull(dequeuedJob);
            job = Guard.IsNotNull(job);
            executeJob = Guard.IsNotNull(executeJob);
            stopwatch = Guard.IsNotNull(stopwatch);
            jobTokenSource = Guard.IsNotNull(jobTokenSource);
            activatorScope = Guard.IsNotNull(activatorScope);
            taskManager = Guard.IsNotNull(taskManager);
            storage = Guard.IsNotNull(storage);

            var environment = context.Daemon.Colony.Environment;

            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> setting background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> to executing", state.FullName, dequeuedJob.JobId, environment);
            // Set to executing
            var isExecuting = await job.ChangeStateAsync(new ExecutingState(context.Daemon.Colony.Name, state.Swarm.Name, state.Name) { Reason = $"Being executed by drone <{state.FullName}>" }, token).ConfigureAwait(false);

            if (isExecuting)
            {
                await job.SaveChangesAsync(true, token).ConfigureAwait(false);

                context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> executing background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", state.FullName, dequeuedJob.JobId, environment);

                // Invoke middleware and job
                await executeJob().ConfigureAwait(false);
                context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> executed background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> in {stopwatch.Elapsed.PrintTotalMs()}", state.FullName, dequeuedJob.JobId, environment);

                // Parse result and set final state
                if (job.State.Name.EqualsNoCase(ExecutingState.StateName))
                {
                    if (jobExecutionContext.Result is Exception ex)
                    {
                        if (ex is OperationCanceledException && jobTokenSource.IsCancellationRequested)
                        {
                            context.Log($"Background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> executed by Drone <{HiveLog.Swarm.DroneNameParam}> was cancelled. Requeueing", dequeuedJob.JobId, environment, state.FullName);
                            await job.ChangeStateAsync(new EnqueuedState() { Reason = $"Drone was cancelled" }, token).ConfigureAwait(false);
                        }
                        else
                        {
                            context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> executed by Drone <{HiveLog.Swarm.DroneNameParam}> failed with exception. Setting failed state", ex, dequeuedJob.JobId, environment, state.FullName);
                            await job.ChangeStateAsync(new FailedState(ex) { Reason = $"Job execution result was exception" }, token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        stopwatch.Stop();
                        await job.ChangeStateAsync(new SucceededState(jobExecutionContext.Duration, stopwatch.Elapsed, DateTime.UtcNow - job.CreatedAtUtc, jobExecutionContext.Result)
                        {
                            Reason = $"Job executed without throwing exception"
                        }, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not longer in executing state after being executed by Drone <{HiveLog.Swarm.DroneNameParam}>. State now is <{HiveLog.Job.StateParam}>", dequeuedJob.JobId, environment, state.FullName, job.State.Name);
                }
            }
            else
            {
                context.Log(LogLevel.Warning, $"Drone <{HiveLog.Swarm.DroneNameParam}> tried setting background job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> to executing but state was transitioned into <{HiveLog.Job.StateParam}>", state.FullName, dequeuedJob.JobId, environment, job.State.Name);
            }

            // Save changes and release job
            await job.SaveChangesAsync(false, token).ConfigureAwait(false);
            await dequeuedJob.CompleteAsync(token);
        }
        /// <inheritdoc/>
        protected override async Task HandleErrorAsync(IDaemonExecutionContext context, IDroneState<IBackgroundJobWorkerSwarmHostOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, Exception exception, CancellationToken token)
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
        /// <inheritdoc/>
        protected override void ValidateAndThrowIfInvalid(BackgroundJobWorkerSwarmHostOptions options) => BackgroundJobWorkerSwarmHostOptionsValidationProfile.Instance.Validate(Guard.IsNotNull(options), null).ThrowOnValidationErrors();
    }
}
