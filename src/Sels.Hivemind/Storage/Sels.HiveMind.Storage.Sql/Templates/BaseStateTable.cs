using Sels.Core.Extensions.DateTimes;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;
using Sels.Core.Extensions;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class for tables that contains the states that jobs can be in.
    /// </summary>
    public abstract class BaseStateTable : BaseIdTable<long>
    {
        /// <inheritdoc cref="IJobState.Name"/>
        public string? Name { get; set; }
        /// <inheritdoc cref="JobStateStorageData.OriginalTypeName"/>
        public string? OriginalType { get; set; }
        /// <inheritdoc cref="IJobState.ElectedDateUtc"/>
        public DateTime ElectedDate { get; set; }

        /// <inheritdoc cref="IJobState.Reason"/>
        public string? Reason { get; set; }
        /// <summary>
        /// Indicates the the state is the current state of the linked background job.
        /// </summary>
        public bool IsCurrent { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The instance to construct from</param>
        public BaseStateTable(JobStateStorageData data)
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
        public BaseStateTable()
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
