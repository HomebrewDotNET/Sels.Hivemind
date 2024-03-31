using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers.BackgroundJob
{
    /// <summary>
    /// Handler that enqueues background jobs when they get elected to <see cref="EnqueuedState"/>.
    /// </summary>
    public class BackgroundJobProcessTrigger : IBackgroundJobFinalStateElectedEventHandler, IBackgroundJobLockTimedOutEventHandler
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IJobQueueProvider _queueProvider;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => null; // Always go last just in case other handlers throw exceptions

        /// <inheritdoc cref="BackgroundJobProcessTrigger"/>
        /// <param name="queueProvider">Provider used to resolve job queues</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobProcessTrigger(IJobQueueProvider queueProvider, ILogger<BackgroundJobProcessTrigger> logger = null)
        {
            _queueProvider = queueProvider.ValidateArgument(nameof(queueProvider));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected BackgroundJobProcessTrigger()
        {
                
        }

        /// <inheritdoc/>
        public virtual async Task HandleAsync(IEventListenerContext context, BackgroundJobFinalStateElectedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            if(@event.FinalState is EnqueuedState enqueuedState)
            {
                var job = @event.Job;

                _logger.Log($"Enqueueing background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> in queue <{HiveLog.Job.Queue}> with a priority of <{HiveLog.Job.Priority}> for processing", job.Id, job.Environment, job.Queue, job.Priority);

                await using (var resolvedQueue = await _queueProvider.GetQueueAsync(job.Environment, token).ConfigureAwait(false))
                {
                    var queue = resolvedQueue.Component;
                    await context.WaitForCommitAsync().ConfigureAwait(false); // Wait for other handlers to commit first
                    await queue.EnqueueAsync(HiveMindConstants.Queue.BackgroundJobProcessQueueType, job.Queue, job.Id, enqueuedState.DelayedToUtc ?? DateTime.UtcNow, job.ExecutionId, job.Priority, @event.Connection, token).ConfigureAwait(false);
                    _logger.Log($"Enqueued background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> in queue <{HiveLog.Job.Queue}> with a priority of <{HiveLog.Job.Priority}> for processing", job.Id, job.Environment, job.Queue, job.Priority);
                }
            }
        }
        
        /// <inheritdoc/>
        public Task HandleAsync(IEventListenerContext context, BackgroundJobLockTimedOutEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var job = @event.Job;
            if (job.State is ExecutingState)
            {
                _logger.Log($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> was processing when it timed out. Rescheduling", job.Id, job.Environment);
               
                return job.ChangeStateAsync(new EnqueuedState() { Reason = "Job timed out while processing" }, token);
            }

            return Task.CompletedTask;
        }
    }
}
