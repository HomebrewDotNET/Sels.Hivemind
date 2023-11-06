using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Job
{
    /// <summary>
    /// Defines on what of a background job a condition is placed on.
    /// </summary>
    public enum QueryBackgroundJobConditionTarget
    {
        /// <summary>
        /// Condition is placed on the queue of a background job.
        /// </summary>
        Queue = 0,
        /// <summary>
        /// Condition is placed on the priority of a background job.
        /// </summary>
        Priority = 1,
        /// <summary>
        /// Condition is placed on the creation date of a background job.
        /// </summary>
        CreatedAt = 2,
        /// <summary>
        /// Condition is placed on the last modification date of a background job.
        /// </summary>
        ModifiedAt = 3,
        /// <summary>
        /// Condition is placed on the current state of a background job.
        /// </summary>
        CurrentState = 4,
        /// <summary>
        /// Condition is placed on a past state of a background job.
        /// </summary>
        PastState = 5,
        /// <summary>
        /// Condition is placed on a property of a background job.
        /// </summary>
        Property = 6,
        /// <summary>
        /// Condition is placed on the current holder of a lock on a background job.
        /// </summary>
        LockedBy = 7
    }
}
