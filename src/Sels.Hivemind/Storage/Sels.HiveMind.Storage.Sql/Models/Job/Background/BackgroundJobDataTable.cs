using Dapper;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to the table that contains the unqueryable data attached to a background job.
    /// </summary>
    public class BackgroundJobDataTable : BaseDataTable
    {
        /// <summary>
        /// The id of the background job this data is attached to.
        /// </summary>
        public long BackgroundJobId { get; set; }

        /// <inheritdoc/>
        public override DynamicParameters ToCreateParameters()
        {
            var parameters = base.ToCreateParameters();
            parameters.AddBackgroundJobId(BackgroundJobId, nameof(BackgroundJobId));
            return parameters;
        }
    }
}
