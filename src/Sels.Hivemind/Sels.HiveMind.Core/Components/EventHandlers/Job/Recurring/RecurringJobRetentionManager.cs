using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job.Recurring;
using Sels.HiveMind.Job.Recurring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers.Job.Recurring
{
    /// <summary>
    /// Event handler that manages the retention of states and logs from recurring jobs.
    /// </summary>
    public class RecurringJobRetentionManager : IRecurringJobUpdatedEventHandler
    {
        // Fields
        private readonly ILogger? _logger;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => null;

        /// <inheritdoc cref="RecurringJobRetentionManager"/>
        /// <param name="logger">Optional logger for tracing</param>
        public RecurringJobRetentionManager(ILogger<RecurringJobRetentionManager>? logger = null)
        {
            _logger = logger;
        }
        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected RecurringJobRetentionManager()
        {

        }

        /// <inheritdoc/>
        public virtual async Task HandleAsync(IEventListenerContext context, RecurringJobUpdatedEvent @event, CancellationToken token)
        {
            context = Guard.IsNotNull(context);
            @event = Guard.IsNotNull(@event);

            var job = @event.Job;
            var connection = @event.Connection;

            _logger.Log($"Checking if retention needs to be applied to recurring job <{HiveLog.Job.Id}>", job.Id);

            var stateRetention = job.Settings.StateRetentionMode;
            var logRetention = job.Settings.LogRetentionMode;
            var stateRetentionAmount = job.Settings.StateRetentionAmount <= 0 ? 1 : job.Settings.StateRetentionAmount;
            var logRetentionAmount = job.Settings.LogRetentionAmount <= 0 ? 1 : job.Settings.LogRetentionAmount;

            if(stateRetention == RecurringJobRetentionMode.KeepAll && logRetention == RecurringJobRetentionMode.KeepAll)
            {
                _logger.Log($"Retention mode is set to keep all on recurring job <{HiveLog.Job.Id}>, no retention will be applied", job.Id);
                return;
            }
            else
            {
                _logger.Debug($"Retention mode is set to {stateRetention} for the states and {logRetention} for the logs on recurring job <{HiveLog.Job.Id}>. Apllying retention", job.Id);
                var (deletesStates, deletedLogs) = await connection.Storage.ApplyRetention(connection, job.Id, stateRetention, stateRetentionAmount, logRetention, logRetentionAmount, token).ConfigureAwait(false);
                _logger.Log($"Applied retention to recurring job <{HiveLog.Job.Id}>. Deleted {deletesStates} states and {deletedLogs} logs", job.Id);
            }
        }
    }
}
