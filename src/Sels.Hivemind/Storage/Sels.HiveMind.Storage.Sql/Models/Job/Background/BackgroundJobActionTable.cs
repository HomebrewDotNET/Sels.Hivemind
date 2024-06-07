using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to the table that contains the pending actions to execute on background jobs.
    /// </summary>
    public class BackgroundJobActionTable : BaseJobActionTable
    {
        // Properties
        /// <summary>
        /// The id of the background job the action is to be executed on.
        /// </summary>
        public long BackgroundJobId { get; set; }
        /// <inheritdoc/>
        public override string ComponentId { get => BackgroundJobId.ToString(); set => BackgroundJobId = value.ConvertTo<long>(); }


        /// <inheritdoc cref="BackgroundJobActionTable"/>
        public BackgroundJobActionTable()
        {

        }

        /// <inheritdoc cref="BackgroundJobActionTable"/>
        /// <param name="action">The instance to copy the state from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public BackgroundJobActionTable(ActionInfo action, HiveMindOptions options, IMemoryCache? cache) : base(action, options, cache)
        {

        }

        /// <inheritdoc/>
        public override DynamicParameters ToCreateParameters()
        {
            var parameters = base.ToCreateParameters();
            parameters.AddBackgroundJobId(BackgroundJobId, nameof(BackgroundJobId));
            return parameters;
        }
    }
}
