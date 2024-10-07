using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Colony
{
    /// <summary>
    /// Defines on what of a colony daemon a condition is placed on.
    /// </summary>
    public enum QueryColonyDaemonConditionTarget
    {
        /// <summary>
        /// Condition is placed on the name of a colony daemon.
        /// </summary>
        Name = 0,
        /// <summary>
        /// Condition is placed on the status of a colony daemon.
        /// </summary>
        Status = 1,
        /// <summary>
        /// Condition is placed on the creation date of a colony daemon.
        /// </summary>
        CreatedAt = 2,
        /// <summary>
        /// Condition is placed on the last modification date of a colony daemon.
        /// </summary>
        ModifiedAt = 3,
        /// <summary>
        /// Condition is placed on a property of a colony daemon.
        /// </summary>
        Property = 4
    }
}
