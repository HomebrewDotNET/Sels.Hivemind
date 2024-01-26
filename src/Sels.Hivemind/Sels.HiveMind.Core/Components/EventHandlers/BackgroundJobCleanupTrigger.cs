using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Sels.HiveMind.EventHandlers
{
    /// <summary>
    /// Handler that places completed background jobs on the cleanup queue.
    /// </summary>
    public class BackgroundJobCleanupTrigger : IBackgroundJobStateAppliedEventHandler, IBackgroundJobFinalStateElectedEventHandler
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IJobQueueProvider _queueProvider;
        private readonly IOptionsSnapshot<HiveMindOptions> _options;

        // Properties
        /// <inheritdoc/>
        public ushort? Priority => null; // Always go last just in case other handlers throw exceptions

        /// <inheritdoc cref="BackgroundJobProcessTrigger"/>
        /// <param name="options">Used to access the HiveMind options for completed job retention</param>
        /// <param name="queueProvider">Provider used to resolve job queues</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobCleanupTrigger(IOptionsSnapshot<HiveMindOptions> options, IJobQueueProvider queueProvider, ILogger<BackgroundJobProcessTrigger> logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _queueProvider = queueProvider.ValidateArgument(nameof(queueProvider));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected BackgroundJobCleanupTrigger()
        {
            
        }

        /// <inheritdoc/>
        public virtual async Task HandleAsync(IEventListenerContext context, BackgroundJobFinalStateElectedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var job = @event.Job;
            var options = _options.Get(job.Environment);

            if (!options.CompletedBackgroundJobRetention.HasValue) return;

            if (@event.FinalState.Name.In(options.CompletedBackgroundJobStateNames))
            {
                _logger.Log($"Enqueueing background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> in queue <{HiveLog.Job.Queue}> with a priority of <{HiveLog.Job.Priority}> for cleanup", job.Id, job.Environment, job.Queue, job.Priority);

                await using (var resolvedQueue = await _queueProvider.GetQueueAsync(job.Environment, token).ConfigureAwait(false))
                {
                    var queue = resolvedQueue.Component;
                    await context.WaitForCommitAsync().ConfigureAwait(false); // Wait for other handlers to commit first
                    await queue.EnqueueAsync(HiveMindConstants.Queue.BackgroundJobCleanupQueueType, job.Queue, job.Id, DateTime.UtcNow.Add(options.CompletedBackgroundJobRetention.Value), job.ExecutionId, job.Priority, @event.Connection, token);
                    _logger.Log($"Enqueued background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> in queue <{HiveLog.Job.Queue}> with a priority of <{HiveLog.Job.Priority}> for cleanup", job.Id, job.Environment, job.Queue, job.Priority);
                }
            }
        }

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, BackgroundJobStateAppliedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var job = @event.Job;
            var options = _options.Get(job.Environment);

            if (!options.CompletedBackgroundJobRetention.HasValue) return Task.CompletedTask;

            // Save cleanup state
            if (@event.AppliedState.Name.In(options.CompletedBackgroundJobStateNames))
            {
                var cleanupTriggered = job.GetPropertyOrSet(HiveMindConstants.Job.Properties.CleanupTriggered, () => false);
                if (!cleanupTriggered) job.SetProperty(HiveMindConstants.Job.Properties.CleanupTriggered, true);

                job.SetProperty(HiveMindConstants.Job.Properties.CleanupRetention, options.CompletedBackgroundJobRetention);
            }
            // Remove cleanup state if completed job state was unapplied and wasn't persisted before
            else if (@event.UnappliedState.Name.In(options.CompletedBackgroundJobStateNames) && job.ChangeTracker.NewStates.Contains(@event.UnappliedState))
            {
                job.SetProperty(HiveMindConstants.Job.Properties.CleanupTriggered, false);
            }

            return Task.CompletedTask;
        }
    }
}
