using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.Core.Extensions;
using Sels.Core.Extensions.DateTimes;
using Dapper;
using static Sels.HiveMind.HiveLog;
using System.Data;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to the table that contains the state history of background jobs.
    /// </summary>
    public class BackgroundJobStateTable : BaseStateTable
    {
        /// <summary>
        /// The id of the background job the state is linked to.
        /// </summary>
        public long BackgroundJobId { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The instance to construct from</param>
        public BackgroundJobStateTable(JobStateStorageData data) : base(data)
        {
        }

        /// <summary>
        /// Creates a new instances.
        /// </summary>
        public BackgroundJobStateTable()
        {

        }

        /// <inheritdoc/>
        public override DynamicParameters ToCreateParameters()
        {
            var parameters = base.ToCreateParameters();
            parameters.AddBackgroundJobId(BackgroundJobId, nameof(BackgroundJobId));
            return parameters;
        }
        /// <inheritdoc/>
        public override void AppendCreateParameters(DynamicParameters parameters, string suffix)
        {
            base.AppendCreateParameters(parameters, suffix);
            parameters.AddBackgroundJobId(BackgroundJobId, $"{nameof(BackgroundJobId)}{suffix}");
        }
    }
}
