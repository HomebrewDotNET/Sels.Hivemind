using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Requests.Job;
using Sels.HiveMind.Schedule;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.RequestHandlers.RecurringJob
{
    /// <summary>
    /// Moves recurring jobs in the <see cref="SchedulingState"/> to the <see cref="EnqueuedState"/> based on the configured schedule.
    /// </summary>
    public class RecurringJobScheduler : IRecurringJobStateElectionRequestHandler
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IIntervalProvider _intervalProvider;
        private readonly ICalendarProvider _calendarProvider;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => 10;

        /// <inheritdoc cref="RecurringJobScheduler"/>
        /// <param name="intervalProvider">Used to resolve intervals</param>
        /// <param name="calendarProvider">Used to resolve calendars</param>
        /// <param name="logger">Optional logger for tracing</param>
        public RecurringJobScheduler(IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ILogger<RecurringJobScheduler> logger = null)
        {
            _intervalProvider = intervalProvider.ValidateArgument(nameof(intervalProvider));
            _calendarProvider = calendarProvider.ValidateArgument(nameof(calendarProvider));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected RecurringJobScheduler()
        {

        }

        /// <inheritdoc/>
        public virtual async Task<RequestResponse<IRecurringJobState>> TryRespondAsync(IRequestHandlerContext context, RecurringJobStateElectionRequest request, CancellationToken token)
        {
            if(request.ElectedState is SchedulingState)
            {
                _logger.Log($"Scheduling recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", request.Job.Id, request.Job.Environment);

                var settigns = request.Job.Settings;

                var dateToSchedule = DateTime.Now;
                switch (settigns.ScheduleTime)
                {
                    case ScheduleTime.LastScheduledDate when request.Job.ExpectedExecutionDate.HasValue:
                        dateToSchedule = request.Job.ExpectedExecutionDate.Value;
                        break;
                    case ScheduleTime.StartedDate when request.Job.LastStartedDate.HasValue:
                        dateToSchedule = request.Job.LastStartedDate.Value;
                        break;
                    case ScheduleTime.CompletedDate when request.Job.LastCompletedDate.HasValue:
                        dateToSchedule = request.Job.LastCompletedDate.Value;
                        break;
                }

                var nextExecutionDate = await request.Job.Schedule.GetNextScheduleDateAsync(dateToSchedule, settigns.MaxScheduleTries, settigns.AlwaysUseInterval, _intervalProvider, _calendarProvider, _logger, token).ConfigureAwait(false);

                _logger.Log($"Enqueueing recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> to be executed at <{nextExecutionDate}>", request.Job.Id, request.Job.Environment);

                return RequestResponse<IRecurringJobState>.Success(new EnqueuedState(nextExecutionDate) { Reason = "Recurring job was moved to scheduling state" });
            }

            return RequestResponse<IRecurringJobState>.Reject();
        }
    }
}
