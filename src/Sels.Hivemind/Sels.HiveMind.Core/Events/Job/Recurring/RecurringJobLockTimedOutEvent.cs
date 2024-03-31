using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a lock on a recurring job timed out. Job can be modified before being released.
    /// </summary>
    public class RecurringJobLockTimedOutEvent
    {
        /// <summary>
        /// The job where the lock timed out on.
        /// </summary>
        public IWriteableRecurringJob Job { get; }

        /// <inheritdoc cref="RecurringJobLockTimedOutEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        public RecurringJobLockTimedOutEvent(IWriteableRecurringJob job)
        {
            Job = job;
        }
    }
}
