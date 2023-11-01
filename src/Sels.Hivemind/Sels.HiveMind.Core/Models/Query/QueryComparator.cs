using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Defines how 2 values should be compared in a query.
    /// </summary>
    public enum QueryComparator
    {
        /// <summary>
        /// Values should be equal.
        /// </summary>
        Equals = 0,
        /// <summary>
        /// Value of the left side should be greater than the value on the right side.
        /// </summary>
        GreaterThan = 1,
        /// <summary>
        /// Value of the left side should be lesser than the value on the right side. 
        /// </summary>
        LesserThan = 2,
        /// <summary>
        /// Value of the left side should be greater or equal to the value on the right side.
        /// </summary>
        GreaterOrEqualTo = 3,
        /// <summary>
        /// Value of the left side should be lesser or equal to the value on the right side.
        /// </summary>
        LesserOrEqualTo = 4,
        /// <summary>
        /// Value on the left side should be like the value on the right side. Makes use of wildcards.
        /// </summary>
        Like = 5,
        /// <summary>
        /// Value on the left side should be in a list of values on the right side.
        /// </summary>
        In = 6
    }
}
