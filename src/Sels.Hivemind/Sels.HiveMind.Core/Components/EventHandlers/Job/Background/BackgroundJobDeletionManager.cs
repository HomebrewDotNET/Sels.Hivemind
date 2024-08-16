using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Events.Job.Background;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.State.Background;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Sels.HiveMind.EventHandlers.Job.Background
{
    /// <summary>
    /// Handler that places completed background jobs on the cleanup queue.
    /// </summary>
    public class BackgroundJobDeletionManager : IBackgroundJobStateAppliedEventHandler
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IJobQueueProvider _queueProvider;
        private readonly IOptionsSnapshot<HiveMindOptions> _options;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => null; // Always go last just in case other handlers throw exceptions

        /// <inheritdoc cref="BackgroundJobProcessTrigger"/>
        /// <param name="options">Used to access the HiveMind options for completed job retention</param>
        /// <param name="queueProvider">Provider used to resolve job queues</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobDeletionManager(IOptionsSnapshot<HiveMindOptions> options, IJobQueueProvider queueProvider, ILogger<BackgroundJobProcessTrigger> logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _queueProvider = queueProvider.ValidateArgument(nameof(queueProvider));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected BackgroundJobDeletionManager()
        {

        }

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, BackgroundJobStateAppliedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var job = @event.Job;
            var options = _options.Get(job.Environment);

            // Deletion disabled
            if (!options.CompletedBackgroundJobRetention.HasValue) return Task.CompletedTask;

            // Mark for deletion
            if (@event.AppliedState.Name.In(options.CompletedBackgroundJobStateNames) || @event.AppliedState.Name.In(DeletedState.StateName, SystemDeletingState.StateName, SystemDeletedState.StateName))
            {
                job.SetProperty(HiveMindConstants.Job.Properties.MarkedForDeletion, true);
            }
            // Remove flag if now allowed to delete
            else if (job.Properties.ContainsKey(HiveMindConstants.Job.Properties.MarkedForDeletion))
            {
                job.RemoveProperty(HiveMindConstants.Job.Properties.MarkedForDeletion);
            }

            return Task.CompletedTask;
        }
    }
}
