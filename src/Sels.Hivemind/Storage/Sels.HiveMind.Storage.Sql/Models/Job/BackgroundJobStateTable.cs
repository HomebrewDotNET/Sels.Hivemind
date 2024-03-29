﻿using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.Core.Extensions;
using Sels.Core.Extensions.DateTimes;

namespace Sels.HiveMind.Storage.Sql.Job
{
    /// <summary>
    /// Model that maps to the table that contains the state history of background jobs.
    /// </summary>
    public class BackgroundJobStateTable : BaseIdTable
    {
        /// <inheritdoc cref="IBackgroundJobState.Name"/>
        public string? Name { get; set; }
        /// <inheritdoc cref="JobStateStorageData.OriginalTypeName"/>
        public string? OriginalType { get; set; }
        /// <inheritdoc cref="IBackgroundJobState.ElectedDateUtc"/>
        public DateTime ElectedDate { get; set; }

        /// <inheritdoc cref="IBackgroundJobState.Reason"/>
        public string? Reason { get; set; }
        /// <summary>
        /// The id of the background job the id is linked to.
        /// </summary>
        public long BackgroundJobId { get; set; }
        /// <summary>
        /// Indicates the the state is the current state of the linked background job.
        /// </summary>
        public bool IsCurrent { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The instance to construct from</param>
        public BackgroundJobStateTable(JobStateStorageData data)
        {
            data.ValidateArgument(nameof(data));
            Name = data.Name;
            OriginalType = data.OriginalTypeName;
            ElectedDate = data.ElectedDateUtc.ToUniversalTime();
            Reason = data.Reason;
        }

        /// <summary>
        /// Creates a new instances.
        /// </summary>
        public BackgroundJobStateTable()
        {
            
        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public JobStateStorageData ToStorageFormat() => new JobStateStorageData()
        {
            Name = Name,
            OriginalTypeName = OriginalType,
            ElectedDateUtc = ElectedDate.AsUtc(),
            Reason = Reason
        };
    }
}
