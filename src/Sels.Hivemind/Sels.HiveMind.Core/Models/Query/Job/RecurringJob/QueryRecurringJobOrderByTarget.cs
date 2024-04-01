using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Query.Job
{
    /// <summary>
    /// Defines what to sort a query on recurring jobs by.
    /// </summary>
    public enum QueryRecurringJobOrderByTarget
    {
        /// <summary>
        /// Order by the the recurring job id.
        /// </summary>
        Id = 0,
        /// <summary>
        /// Order by the recurring job creation date.
        /// </summary>
        CreatedAt = 1,
        /// <summary>
        /// Order by the recurring job last modification date.
        /// </summary>
        ModifiedAt = 2,
        /// <summary>
        /// Order by the recurring job queue.
        /// </summary>
        Queue = 4,
        /// <summary>
        /// Order by the recurring job priority.
        /// </summary>
        Priority = 5,
        /// <summary>
        /// Order by the expected execution date of the recurring job.
        /// </summary>
        ExpectedExecutionDate = 6,
        /// <summary>
        /// Order by last started date of the recurring job.
        /// </summary>
        LastStartedDate = 7,
        /// <summary>
        /// Order by last completed date of the recurring job.
        /// </summary>
        LastCompletedDate = 8,
    }
}
