using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;
using Sels.Core.Extensions.DateTimes;
using Dapper;
using static Sels.HiveMind.HiveLog;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the state history of recurring jobs.
    /// </summary>
    public class RecurringJobStateTable : BaseStateTable
    {
        /// <summary>
        /// The id of the background job the state is linked to.
        /// </summary>
        public string RecurringJobId { get; set; }
        /// <inheritdoc cref="IRecurringJobState.Sequence"/>
        public long Sequence { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The instance to construct from</param>
        public RecurringJobStateTable(RecurringJobStateStorageData data) : base(data)
        {
            Sequence = data.Sequence;
        }

        /// <summary>
        /// Creates a new instances.
        /// </summary>
        public RecurringJobStateTable()
        {

        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public new RecurringJobStateStorageData ToStorageFormat() => new RecurringJobStateStorageData()
        {
            Name = Name,
            Sequence = Sequence,
            OriginalTypeName = OriginalType,
            ElectedDateUtc = ElectedDate.AsUtc(),
            Reason = Reason
        };

        /// <summary>
        /// Creates dapper parameters to insert the current instance.
        /// </summary>
        /// <returns>Dapper parameters to insert the current instance</returns>
        public override DynamicParameters ToCreateParameters()
        {
            var parameters = base.ToCreateParameters();
            parameters.AddRecurringJobId(RecurringJobId, nameof(RecurringJobId));
            parameters.Add(nameof(Sequence), Sequence, System.Data.DbType.Int64, System.Data.ParameterDirection.Input);
            return parameters;
        }
    }
}
