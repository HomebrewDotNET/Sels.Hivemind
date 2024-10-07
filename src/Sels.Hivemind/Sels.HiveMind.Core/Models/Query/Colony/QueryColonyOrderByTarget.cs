using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Colony
{
    /// <summary>
    /// Defines what to sort a query on colonies by.
    /// </summary>
    public enum QueryColonyOrderByTarget
    {
        /// <summary>
        /// Order by the id.
        /// </summary>
        Id = 0,
        /// <summary>
        /// Order by the name.
        /// </summary>
        Name = 1,
        /// <summary>
        /// Order by the creation date.
        /// </summary>
        CreatedAt = 2,
        /// <summary>
        /// Order by the last modification date.
        /// </summary>
        ModifiedAt = 3,
        /// <summary>
        /// Order by the status.
        /// </summary>
        Status = 4
    }
}
