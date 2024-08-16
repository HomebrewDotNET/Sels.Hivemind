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
using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Colony.Swarm.Job.Recurring;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Job;
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
using System.Threading.Tasks;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.State.Recurring;

namespace Sels.HiveMind.Colony.Swarm.Job.Recurring
{
    /// <summary>
    /// A swarm that executes pending recurring jobs.
    /// </summary>
    public class RecurringJobWorkerSwarmHost : WorkerSwarmHost<RecurringJobWorkerSwarmHostOptions, IRecurringJobExecutionContext, IRecurringJobMiddleware, IWriteableRecurringJob, IReadOnlyRecurringJob, ILockedRecurringJob, IRecurringJobChangeTracker, IRecurringJobState, IRecurringJobAction, QueryRecurringJobOrderByTarget?, IRecurringJobWorkerSwarmHostOptions, RecurringJobWorkerSwarmDefaultHostOptions>
    {
        // Fields
        /// <inheritdoc cref="JobSwarmHost{TReadOnlyJob, TLockedJob, TChangeTracker, TState, TAction, TSortTarget, TOptions, TDefaultOptions}._client"/>
        protected new readonly IRecurringJobClient _client;

        /// <inheritdoc/>
        protected override string SwarmPrefix => "RecurringJobWorker.";

        /// <inheritdoc cref="RecurringJobWorkerSwarmHost"/>
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
        public RecurringJobWorkerSwarmHost(RecurringJobWorkerSwarmHostOptions initialOptions, IRecurringJobClient client, string queueType, IOptionsMonitor<RecurringJobWorkerSwarmDefaultHostOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
        : base(initialOptions, client, queueType, defaultOptions, jobQueueProvider, schedulerProvider, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _client = Guard.IsNotNull(client);
        }
        /// <inheritdoc/>
        protected override async Task ProcessAsync(IDaemonExecutionContext context, IDroneState<IRecurringJobWorkerSwarmHostOptions> state, IServiceProvider serviceProvider, IDequeuedJob dequeuedJob, CancellationToken token)
        {
            using var loggerScope = _logger.TryBeginScope(new Dictionary<string, object>
            {
                { HiveLog.Job.Type, HiveLog.Job.RecurringJobType }
            });
            await base.ProcessAsync(context, state, serviceProvider, dequeuedJob, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override Task<(bool CanProcess, TimeSpan? Delay)> CanBeProcessedAsync(IDaemonExecutionContext context, IDroneState<IRecurringJobWorkerSwarmHostOptions> state, IDequeuedJob job, IReadOnlyRecurringJob recurringJob, HiveMindOptions options,  CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            recurringJob.ValidateArgument(nameof(recurringJob));

            TimeSpan? delay = null;

            // Execution matches
            if (!job.ExecutionId.Equals(recurringJob.ExecutionId))
            {
                context.Log(LogLevel.Warning, $"Execution id of dequeued job does not match execution id of recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>. Can't process", recurringJob.Id, recurringJob.Environment);
                return Task.FromResult<(bool CanProcess, TimeSpan? Delay)>((false, delay));
            }
            else if (!(recurringJob.State is EnqueuedState))
            {
                context.Log(LogLevel.Warning, $"State of recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not <{nameof(EnqueuedState)}> but <{HiveLog.Job.StateParam}>. Can't process", recurringJob.Id, recurringJob.Environment, recurringJob.State);
                return Task.FromResult<(bool CanProcess, TimeSpan? Delay)>((false, delay));
            }

            return Task.FromResult<(bool CanProcess, TimeSpan? Delay)>((true, delay));
        }
        /// <inheritdoc/>
        protected override async Task<(bool CanProcess, TimeSpan? Delay)> CanBeProcessedAsync(IDaemonExecutionContext context, IDroneState<IRecurringJobWorkerSwarmHostOptions> state, IDequeuedJob dequeudJob, ILockedRecurringJob recurringJob, HiveMindOptions options, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            recurringJob.ValidateArgument(nameof(recurringJob));
            recurringJob.ValidateArgument(nameof(recurringJob));

            if(await CanBeProcessedAsync(context, state, dequeudJob, recurringJob.CastTo<IReadOnlyRecurringJob>(), options, token) is (false, var delay))
            {
                return (false, delay);
            }

            if (recurringJob.Settings.CanMisfire && recurringJob.ExpectedExecutionDate.HasValue && ((DateTime.Now - recurringJob.ExpectedExecutionDate.Value).Duration() > recurringJob.Settings.MisfireThreshold))
            {
                var latency = (DateTime.Now - recurringJob.ExpectedExecutionDate.Value).Duration();

                context.Log(LogLevel.Warning, $"Recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> misfired because the latancy <{latency}> is higher than the configured max allowed latency of <{recurringJob.Settings.MisfireThreshold}>. Can't process", recurringJob.Id, recurringJob.Environment, recurringJob.State);
            
                await recurringJob.ChangeStateAsync(new MisfiredState(latency) { Reason = $"Misfired with a latency of <{latency}> which was higher than the configured max allowed latency of <{recurringJob.Settings.MisfireThreshold}>" }, token).ConfigureAwait(false);
                await recurringJob.UpdateAsync(false, token).ConfigureAwait(false);
                return (false, null);
            }

            return (true, null);
        }
        /// <inheritdoc/>
        protected override IRecurringJobExecutionContext CreateContext(IDaemonExecutionContext context, IDroneState<IRecurringJobWorkerSwarmHostOptions> state, IServiceProvider serviceProvider, ILockedRecurringJob job, object? instance, object[] arguments, CancellationTokenSource tokenSource, LogLevel logLevel, TimeSpan logFlushInterval, TimeSpan actionPollingInterval, int actionFetchLimit, IActivatorScope activatorScope, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory)
        => new RecurringJobExecutionContext(context, state, job, instance, arguments, tokenSource, logLevel, logFlushInterval, actionPollingInterval, actionFetchLimit, context.ServiceProvider.GetRequiredService<IRecurringJobService>(), activatorScope, taskManager, storage, loggerFactory);
        /// <inheritdoc/>
        protected override async Task ExecuteJobAsync(IDaemonExecutionContext context, IDroneState<IRecurringJobWorkerSwarmHostOptions> state, IServiceProvider serviceProvider, IRecurringJobExecutionContext jobExecutionContext, IDequeuedJob dequeuedJob, ILockedRecurringJob job, Func<Task> executeJob, Stopwatch stopwatch, CancellationTokenSource jobTokenSource, IActivatorScope activatorScope, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory, CancellationToken token)
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

            context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.NameParam}> setting recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> to executing", state.FullName, dequeuedJob.JobId, environment);
            // Set to executing
            var isExecuting = await job.ChangeStateAsync(new ExecutingState(context.Daemon.Colony.Name, state.Swarm.Name, state.Name) { Reason = $"Being executed by drone <{state.FullName}>" }, token).ConfigureAwait(false);

            if (isExecuting)
            {
                await job.UpdateAsync(true, token).ConfigureAwait(false);

                context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> executing recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}>", state.FullName, dequeuedJob.JobId, environment);

                // Invoke middleware and job
                await executeJob().ConfigureAwait(false);
                context.Log($"Drone <{HiveLog.Swarm.DroneNameParam}> executed recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> in {stopwatch.Elapsed.PrintTotalMs()}", state.FullName, dequeuedJob.JobId, environment);

                // Parse result and set final state
                if (job.State.Name.EqualsNoCase(ExecutingState.StateName))
                {
                    if (jobExecutionContext.Result is Exception ex)
                    {
                        if (ex is OperationCanceledException && jobTokenSource.IsCancellationRequested)
                        {
                            context.Log($"Recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> executed by Drone <{HiveLog.Swarm.DroneNameParam}> was cancelled. Requeueing", dequeuedJob.JobId, environment, state.FullName);
                            await job.ChangeStateAsync(new EnqueuedState() { Reason = $"Drone was cancelled" }, token).ConfigureAwait(false);
                        }
                        else
                        {
                            context.Log(LogLevel.Warning, $"Recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> executed by Drone <{HiveLog.Swarm.DroneNameParam}> failed with exception. Setting failed state", ex, dequeuedJob.JobId, environment, state.FullName);
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
                    context.Log(LogLevel.Warning, $"Recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> is not longer in executing state after being executed by Drone <{HiveLog.Swarm.DroneNameParam}>. State now is <{HiveLog.Job.StateParam}>", dequeuedJob.JobId, environment, state.FullName, job.State.Name);
                }
            }
            else
            {
                context.Log(LogLevel.Warning, $"Drone <{HiveLog.Swarm.DroneNameParam}> tried setting recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> to executing but state was transitioned into <{HiveLog.Job.StateParam}>", state.FullName, dequeuedJob.JobId, environment, job.State.Name);
            }

            // Save changes and release job
            if (job.State.Name.Equals(SystemDeletingState.StateName))
            {
                context.Log(LogLevel.Warning, $"Recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> was moved to the system deleting state after processing. Trigering delete", dequeuedJob.JobId, environment, state.FullName, job.State.Name);

                if (!await job.SystemDeleteAsync(environment).ConfigureAwait(false)) 
                { 
                    context.Log(LogLevel.Warning, $"Failed to delete recurring job {HiveLog.Job.IdParam} in environment <{HiveLog.EnvironmentParam}> after processing. Moving to failed state", dequeuedJob.JobId, state.FullName);

                    await job.ChangeStateAsync(new FailedState($"Could not delete recurring job <{job.Id}>  after processing") { Reason = "Failed deletion"}, token).ConfigureAwait(false);
                }
            }
            
            if(!job.IsDeleted && job.HasLock) await job.UpdateAsync(false, token).ConfigureAwait(false);
            await dequeuedJob.CompleteAsync(token);
        }
        /// <inheritdoc/>
        protected override async Task HandleErrorAsync(IDaemonExecutionContext context, IDroneState<IRecurringJobWorkerSwarmHostOptions> state, IDequeuedJob job, ILockedRecurringJob recurringJob, HiveMindOptions options, Exception exception, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            recurringJob.ValidateArgument(nameof(recurringJob));
            exception.ValidateArgument(nameof(exception));

            await recurringJob.ChangeStateAsync(new FailedState(exception)
            {
                Reason = $"Exception was thrown while drone <{state.FullName}> was handling job"
            }, token).ConfigureAwait(false);
            await recurringJob.UpdateAsync(false, token).ConfigureAwait(false);
            await job.CompleteAsync(token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override void ValidateAndThrowIfInvalid(RecurringJobWorkerSwarmHostOptions options) => RecurringJobWorkerSwarmHostOptionsValidationProfile.Instance.Validate(Guard.IsNotNull(options), null).ThrowOnValidationErrors();
    }
}
