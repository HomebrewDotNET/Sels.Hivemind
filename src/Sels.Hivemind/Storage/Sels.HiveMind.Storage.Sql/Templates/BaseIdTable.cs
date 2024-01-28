using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class that contains the primary key for most tables.
    /// </summary>
    public class BaseIdTable : BaseTable
    {
        /// <summary>
        /// The primary key of the column.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        public BaseIdTable(JobStorageData job) : base(job)
        {
            job.ValidateArgument(nameof(job));

            if (job.Id.HasValue()) Id = job.Id.ConvertTo<long>();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseIdTable()
        {

        }
    }
}
