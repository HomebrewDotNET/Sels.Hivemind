using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Determines the behaviour of a recurring job when it misfires.
    /// </summary>
    public enum MisfireBehaviour
    {
        /// <summary>
        /// The recurring job will be rescheduled using it's schedule.
        /// </summary>
        Schedule = 0,
        /// <summary>
        /// Misfire is treated as an error and the configured retry behaviour will be used.
        /// </summary>
        Retry = 1,
        /// <summary>
        /// Recurring job will be moved to the failed state.
        /// </summary>
        Fail = 2,
        /// <summary>
        /// Recurring job will be moved to the idle state.
        /// </summary>
        Idle = 3,
        /// <summary>
        /// Recurring job will be moved to the misfired state.
        /// </summary>
        Misfire = 4,
        /// <summary>
        /// Job will be permanently deleted from the system.
        /// </summary>
        SystemDelete = 5
    }
}
