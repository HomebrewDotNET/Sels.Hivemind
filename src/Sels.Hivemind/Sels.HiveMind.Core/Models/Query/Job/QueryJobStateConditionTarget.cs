using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Job
{
    /// <summary>
    /// Defines on what of a job state a condition is placed on.
    /// </summary>
    public enum QueryJobStateConditionTarget
    {
        /// <summary>
        /// Condition is placed on the state name.
        /// </summary>
        Name = 0,
        /// <summary>
        /// Condition is placed on the state transition reason.
        /// </summary>
        Reason = 1,
        /// <summary>
        /// Condition is placed on the state elected date.
        /// </summary>
        ElectedDate = 2,
        /// <summary>
        /// Condition is placed on a state property.
        /// </summary>
        Property = 3
    }
}
