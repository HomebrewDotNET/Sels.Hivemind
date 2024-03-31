using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Job has been placed on the queue for processing.
    /// </summary>
    public class EnqueuedState : BaseSharedJobState<EnqueuedState>
    {
        /// <summary>
        /// The date (in utc) after which the job can be picked up from the queue. Will be null when the job can be picked up right away.
        /// </summary>
        public DateTime? DelayedToUtc { get; set; }
        /// <summary>
        /// The date (local time) after which the job can be picked up from the queue. Will be null when the job can be picked up right away.
        /// </summary>
        [IgnoredStateProperty]
        public DateTime? DelayedTo => DelayedToUtc.HasValue ? DelayedToUtc.Value.ToLocalTime() : (DateTime?) null;

        /// <inheritdoc/>
        public EnqueuedState()
        {
            
        }

        /// <inheritdoc/>
        /// <param name="delayedToState"><inheritdoc cref="DelayedTo"/></param>
        public EnqueuedState(DateTime delayedToState)
        {
            DelayedToUtc = delayedToState.ToUniversalTime();
        }
    }
}
