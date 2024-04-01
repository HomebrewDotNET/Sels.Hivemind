using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Job
{
    /// <summary>
    /// Defines what to sort a query on background jobs by.
    /// </summary>
    public enum QueryBackgroundJobOrderByTarget
    {
        /// <summary>
        /// Order by the the background job id.
        /// </summary>
        Id = 0,
        /// <summary>
        /// Order by the background job creation date.
        /// </summary>
        CreatedAt = 1,
        /// <summary>
        /// Order by the background job last modification date.
        /// </summary>
        ModifiedAt = 2,
        /// <summary>
        /// Order by the background job queue.
        /// </summary>
        Queue = 4,
        /// <summary>
        /// Order by the background job priority.
        /// </summary>
        Priority = 5
    }
}
