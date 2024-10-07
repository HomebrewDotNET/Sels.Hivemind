using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Colony
{
    /// <summary>
    /// Defines on what of a colony a condition is placed on.
    /// </summary>
    public enum QueryColonyConditionTarget
    {
        /// <summary>
        /// Condition is placed on the name of a colony.
        /// </summary>
        Name = 0,
        /// <summary>
        /// Condition is placed on the status of a colony.
        /// </summary>
        Status = 1,
        /// <summary>
        /// Condition is placed on the creation date of a colony.
        /// </summary>
        CreatedAt = 2,
        /// <summary>
        /// Condition is placed on the last modification date of a colony.
        /// </summary>
        ModifiedAt = 3,
        /// <summary>
        /// Condition is placed on a property of a colony.
        /// </summary>
        Property = 4,
        /// <summary>
        /// Condition is placed on the daemons of a colony.
        /// </summary>
        Daemon = 5
    }
}
