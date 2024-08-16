using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Determines which date is used to determine the next exection date of a recurring job.
    /// </summary>
    public enum ScheduleTime
    {
        /// <summary>
        /// Uses the time the recurring job was last executed.
        /// </summary>
        CompletedDate = 0,
        /// <summary>
        /// Uses the time the recurring job was last started.
        /// </summary>
        StartedDate = 1,
        /// <summary>
        /// Uses the last time the recurring job was supposed to be scheduled. Won't always be the same as <see cref="StartedDate"/> due to latency.
        /// </summary>
        LastScheduledDate = 2
    }
}
