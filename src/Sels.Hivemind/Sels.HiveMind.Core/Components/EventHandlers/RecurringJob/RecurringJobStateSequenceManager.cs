using Sels.Core.Extensions;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Components.EventHandlers.RecurringJob
{
    /// <summary>
    /// Manages the <see cref="IRecurringJobState.Sequence"/> for recurring jobs
    /// </summary>
    public class RecurringJobStateSequenceManager : IRecurringJobStateAppliedEventHandler
    {
        /// <inheritdoc/>
        public byte? Priority => 0; // Run first

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, RecurringJobStateAppliedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            @event.AppliedState.Sequence = @event.UnappliedState.Sequence + 1;

            return Task.CompletedTask;
        }
    }
}
