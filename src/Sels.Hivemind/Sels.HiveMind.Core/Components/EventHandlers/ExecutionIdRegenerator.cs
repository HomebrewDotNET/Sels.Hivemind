using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers
{
    /// <summary>
    /// Regenerates the execution id of jobs each time they change state.
    /// </summary>
    public class ExecutionIdRegenerator : IBackgroundJobStateAppliedEventHandler
    {
        // Fields
        private readonly ILogger _logger;

        // Properties
        /// <inheritdoc/>
        public ushort? Priority => null;

        /// <inheritdoc cref="ExecutionIdRegenerator"/>
        /// <param name="logger">Optional logger for tracing</param>
        public ExecutionIdRegenerator(ILogger<ExecutionIdRegenerator> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected ExecutionIdRegenerator()
        {

        }

        /// <inheritdoc/>
        public Task HandleAsync(IEventListenerContext context, BackgroundJobStateAppliedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            if(!@event.Job.ChangeTracker.ExecutionIdChanged && @event.Job.ChangeTracker.NewStates.HasValue())
            {
                _logger.Log($"Setting new execution id for background job <{(@event.Job.Id ?? "New")}>");
                @event.Job.RegenerateExecutionId();
            }

            return Task.CompletedTask;
        }
    }
}
