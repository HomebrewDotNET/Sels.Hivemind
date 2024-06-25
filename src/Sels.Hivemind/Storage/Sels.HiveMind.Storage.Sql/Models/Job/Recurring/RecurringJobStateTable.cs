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
        

        /// <summary>
        /// Creates a new instance from <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The instance to construct from</param>
        public RecurringJobStateTable(JobStateStorageData data) : base(data)
        {
        }

        /// <summary>
        /// Creates a new instances.
        /// </summary>
        public RecurringJobStateTable()
        {

        }

        /// <inheritdoc/>
        public override DynamicParameters ToCreateParameters()
        {
            var parameters = base.ToCreateParameters();
            parameters.AddRecurringJobId(RecurringJobId, nameof(RecurringJobId));
            return parameters;
        }
        /// <inheritdoc/>
        public override void AppendCreateParameters(DynamicParameters parameters, string suffix)
        {
            base.AppendCreateParameters(parameters, suffix);
            parameters.AddRecurringJobId(RecurringJobId, $"{nameof(RecurringJobId)}{suffix}");
        }
    }
}
