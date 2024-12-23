﻿using Sels.Core.Extensions;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Events.Job.Background;
using Sels.HiveMind.Events.Job.Recurring;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers
{
    /// <summary>
    /// Manages the <see cref="IJobState.Sequence"/> for jobs
    /// </summary>
    public class JobStateSequenceManager : IRecurringJobStateAppliedEventHandler, IBackgroundJobStateAppliedEventHandler
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
        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, BackgroundJobStateAppliedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            @event.AppliedState.Sequence = @event.UnappliedState.Sequence + 1;

            return Task.CompletedTask;
        }
    }
}
