using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Job
{
    /// <summary>
    /// Defines on what of a job a condition is placed on.
    /// </summary>
    public enum QueryJobConditionTarget
    {
        /// <summary>
        /// Condition is placed on the queue of a job.
        /// </summary>
        Queue = 0,
        /// <summary>
        /// Condition is placed on the priority of a job.
        /// </summary>
        Priority = 1,
        /// <summary>
        /// Condition is placed on the creation date of a job.
        /// </summary>
        CreatedAt = 2,
        /// <summary>
        /// Condition is placed on the last modification date of a job.
        /// </summary>
        ModifiedAt = 3,
        /// <summary>
        /// Condition is placed on the current state of a job.
        /// </summary>
        CurrentState = 4,
        /// <summary>
        /// Condition is placed on a past state of a job.
        /// </summary>
        PastState = 5,
        /// <summary>
        /// Condition is placed on a property of a job.
        /// </summary>
        Property = 6,
        /// <summary>
        /// Condition is placed on the current holder of a lock on a job.
        /// </summary>
        LockedBy = 7,
        /// <summary>
        /// Condition is placed on the id of a job.
        /// </summary>
        Id = 8
    }
}
