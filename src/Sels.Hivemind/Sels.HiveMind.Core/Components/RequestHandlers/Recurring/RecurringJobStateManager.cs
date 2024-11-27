using Microsoft.Extensions.Options;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.Core.Mediator.Event;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Requests.Job;
using Sels.HiveMind.Requests.Job.Recurring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sels.HiveMind.Events.Job.Recurring;
using Sels.HiveMind.Job.State.Recurring;

namespace Sels.HiveMind.RequestHandlers.Recurring
{
    /// <summary>
    /// Request handler that manages the various state changes of recurring jobs.
    /// </summary>
    public class RecurringJobStateManager : IRecurringJobStateElectionRequestHandler, IRecurringJobStateAppliedEventHandler
    {
        // Fields
        /// <summary>
        /// Optional logger for tracing.
        /// </summary>
        protected readonly ILogger? _logger;
        /// <summary>
        /// Used to access the retry options per environment.
        /// </summary>
        protected readonly IOptionsSnapshot<RecurringJobRetryOptions> _options;

        /// <inheritdoc/>
        public byte? Priority => null; // Always go last so other handlers can intercept state changes first.

        /// <inheritdoc cref="RecurringJobStateManager"/>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        public RecurringJobStateManager(IOptionsSnapshot<RecurringJobRetryOptions> options, ILogger<RecurringJobStateManager>? logger = null)
        {
            _logger = logger;
            _options = Guard.IsNotNull(options);
        }

        /// <inheritdoc cref="RecurringJobStateManager"/>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        protected RecurringJobStateManager(IOptionsSnapshot<RecurringJobRetryOptions> options, ILogger? logger)
        {
            _logger = logger;
            _options = Guard.IsNotNull(options);
        }
        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected RecurringJobStateManager()
        {

        }

        /// <inheritdoc/>
        public virtual async Task<RequestResponse<IRecurringJobState?>> TryRespondAsync(IRequestHandlerContext context, RecurringJobStateElectionRequest request, CancellationToken token)
        {
            context = Guard.IsNotNull(context);
            request = Guard.IsNotNull(request);

            var job = request.Job;

            if (request.ElectedState.Name.Equals(MisfiredState.StateName)) // Handle behaviour for misfires
            {
                switch (job.Settings.MisfireBehaviour)
                {
                    case MisfireBehaviour.Retry:
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> misfired. Rescheduling using retry logic", job.Id, job.Environment);

                        var retryState = await TryScheduleRetry(job, null, token).ConfigureAwait(false);
                        if (retryState != null)
                        {
                            _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> moved to state <{HiveLog.Job.StateParam}> to retry misfire", job.Id, job.Environment, retryState.Name);
                            return RequestResponse<IRecurringJobState?>.Success(retryState);
                        }
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> ran out of retries after misfiring. Failing", job.Id, job.Environment);
                        return RequestResponse<IRecurringJobState?>.Success(new FailedState("Ran out of retries after misfiring"));
                    case MisfireBehaviour.Schedule:
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> misfired. Rescheduling using schedule", job.Id, job.Environment);
                        return RequestResponse<IRecurringJobState?>.Success(new SchedulingState()
                        {
                            Reason = "Misfired"
                        });
                    case MisfireBehaviour.Fail:
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> misfired. Failing", job.Id, job.Environment);
                        return RequestResponse<IRecurringJobState?>.Success(new FailedState("Misfired"));
                    case MisfireBehaviour.Idle:
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> misfired. Moving to idle state", job.Id, job.Environment);
                        return RequestResponse<IRecurringJobState?>.Success(new IdleState()
                        {
                            Reason = "Misfired"
                        });
                    case MisfireBehaviour.Misfire:
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> misfired", job.Id, job.Environment);
                        break;
                    case MisfireBehaviour.SystemDelete:
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> misfired. Deleting", job.Id, job.Environment);
                        return RequestResponse<IRecurringJobState?>.Success(new SystemDeletingState()
                        {
                            Reason = "Misfired"
                        });
                    default: throw new NotSupportedException($"Misfire behaviour <{job.Settings.MisfireBehaviour}> is not supported");
                }
            }
            else if (request.ElectedState.Name.EqualsNoCase(FailedState.StateName) && !request.Job.ChangeTracker.NewStates.Any(x => x.Name.EqualsNoCase(MisfiredState.StateName))) // Only handle failures when we aren't dealing with misfire to avoid loops
            {
                var retryState = await TryScheduleRetry(job, null, token).ConfigureAwait(false);
                if (retryState != null)
                {
                    var retryStateAfterFail = await TryScheduleRetry(job, null, token).ConfigureAwait(false);
                    if (retryStateAfterFail != null)
                    {
                        _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> moved to state <{HiveLog.Job.StateParam}> to retry failure", job.Id, job.Environment, retryState.Name);
                        return RequestResponse<IRecurringJobState?>.Success(retryStateAfterFail);
                    }
                }
            }
            else if (request.ElectedState.Name.EqualsNoCase(SucceededState.StateName))
            {
                _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> executed successfully. Rescheduling", job.Id, job.Environment);
                return RequestResponse<IRecurringJobState?>.Success(new SchedulingState()
                {
                    Reason = "Successfully executed"
                });
            }

            return RequestResponse<IRecurringJobState?>.Reject();
        }

        private Task<IRecurringJobState?> TryScheduleRetry(IWriteableRecurringJob job, Exception? exception, CancellationToken token)
        {
            job = Guard.IsNotNull(job);

            var options = _options.Get(job.Environment);
            // Get retry counts
            int currentRetryCount = job.GetPropertyOrSet(HiveMindConstants.Job.Properties.RetryCount, () => 0);
            int totalRetryCount = job.GetPropertyOrSet(HiveMindConstants.Job.Properties.TotalRetryCount, () => currentRetryCount);

            _logger.Debug($"Checking if recurring job can be retried");

            // Check if we can retry
            if (currentRetryCount < (job.Settings.MaxRetryCount ?? options.MaxRetryCount))
            {
                _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> can be retried. Checking if exception is fatal", job.Id, job.Environment);

                if (exception == null || !options.IsFatal(exception))
                {
                    if (job.Settings.RetryUsingSchedule ?? options.RetryUsingSchedule)
                    {
                        if (exception != null) _logger.Log($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> will be retried by rescheduling after failing due to exception <{exception}>", job.Id, job.Environment);
                        else _logger.Log($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> will be retried by rescheduling", job.Id, job.Environment);

                        return new SchedulingState()
                        {
                            Reason = $"Retry {currentRetryCount} of {options.MaxRetryCount}"
                        }.ToTaskResult<IRecurringJobState?>();
                    }

                    var retryDelay = currentRetryCount >= options.RetryTimes.Length ? options.RetryTimes.Last() : options.RetryTimes[currentRetryCount];
                    currentRetryCount++;
                    totalRetryCount++;
                    job.SetProperty(HiveMindConstants.Job.Properties.RetryCount, currentRetryCount);
                    job.SetProperty(HiveMindConstants.Job.Properties.TotalRetryCount, totalRetryCount);

                    if (exception != null) _logger.Log($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> will be retried in <{retryDelay}> after failing due to exception <{exception}>", job.Id, job.Environment);
                    else _logger.Log($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> will be retried in <{retryDelay}>", job.Id, job.Environment);

                    return new EnqueuedState(DateTime.Now.Add(retryDelay))
                    {
                        Reason = $"Retry {currentRetryCount} of {options.MaxRetryCount}"
                    }.ToTaskResult<IRecurringJobState?>();
                }
                else
                {
                    _logger.Warning($"Recurring job <{HiveLog.Job.IdParam}> ran into fatal exception <{exception}>. Job won't be retried", job.Id);
                }
            }

            return Task.FromResult<IRecurringJobState?>(null);
        }

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, RecurringJobStateAppliedEvent @event, CancellationToken token)
        {
            context = Guard.IsNotNull(context);
            @event = Guard.IsNotNull(@event);

            if (@event.AppliedState.Name.Equals(ExecutingState.StateName, StringComparison.OrdinalIgnoreCase))
            {
                // Increment executed amount
                @event.Job.ExecutedAmount++;
                // Set last started date
                @event.Job.LastStartedDateUtc = DateTime.UtcNow;
            }
            else if (@event.AppliedState.Name.In(StringComparer.OrdinalIgnoreCase, SucceededState.StateName, FailedState.StateName))
            {
                // Set last completed date
                @event.Job.LastCompletedDateUtc = DateTime.UtcNow;
            }

            // Reset retry count if job succeeded or if job failed and is transitioning away from it as this could indicate custom handlers handling the faillure.
            if (@event.AppliedState.Name.Equals(SucceededState.StateName, StringComparison.OrdinalIgnoreCase) || (@event.Job.ChangeTracker.NewStates.Any(x => x.Name.EqualsNoCase(FailedState.StateName) && !@event.AppliedState.Name.EqualsNoCase(FailedState.StateName))))
            {               
                @event.Job.SetProperty(HiveMindConstants.Job.Properties.RetryCount, 0);
            }

            // Set expected execution date
            if(@event.AppliedState is EnqueuedState enqueuedState)
            {
                @event.Job.ExpectedExecutionDateUtc = enqueuedState.DelayedToUtc ?? DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }
    }
}
