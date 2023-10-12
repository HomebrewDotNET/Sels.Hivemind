using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class that contains common columns used in other tables.
    /// </summary>
    public class BaseTable
    {
        /// <summary>
        /// When the row was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// When the row was last updated.
        /// </summary>
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        public BaseTable(JobStorageData job)
        {
            job.ValidateArgument(nameof(job));
            CreatedAt = job.CreatedAtUtc;
            ModifiedAt = job.ModifiedAtUtc;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseTable()
        {
            
        }
    }
}
