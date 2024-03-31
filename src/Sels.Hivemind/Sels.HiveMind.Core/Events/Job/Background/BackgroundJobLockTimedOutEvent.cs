using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a lock on a background job timed out. Job can be modified before being released.
    /// </summary>
    public class BackgroundJobLockTimedOutEvent
    {
        /// <summary>
        /// The job where the lock timed out on.
        /// </summary>
        public IWriteableBackgroundJob Job { get; }

        /// <inheritdoc cref="BackgroundJobLockTimedOutEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        public BackgroundJobLockTimedOutEvent(IWriteableBackgroundJob job)
        {
            Job = job;
        }
    }
}
