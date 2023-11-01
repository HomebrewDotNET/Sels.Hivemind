using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Defines how 2 query conditions should be compared.
    /// </summary>
    public enum QueryLogicalOperator
    {
        /// <summary>
        /// The left query condition and the right query condition should both return true.
        /// </summary>
        And = 1,
        /// <summary>
        /// Either the left query condition or the right query condition should return true.
        /// </summary>
        Or = 2
    }
}
