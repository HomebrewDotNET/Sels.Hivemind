using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Conversion.Extensions;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the pending actions to execute on recurring jobs.
    /// </summary>
    public class RecurringJobActionTable : BaseJobActionTable
    {
        // Properties
        /// <summary>
        /// The id of the recurring job the action is to be executed on.
        /// </summary>
        public string RecurringJobId { get; set; }
        /// <inheritdoc/>
        public override string ComponentId { get => RecurringJobId; set => RecurringJobId = value; }


        /// <inheritdoc cref="RecurringJobActionTable"/>
        public RecurringJobActionTable()
        {

        }

        /// <inheritdoc cref="RecurringJobActionTable"/>
        /// <param name="action">The instance to copy the state from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public RecurringJobActionTable(ActionInfo action, HiveMindOptions options, IMemoryCache? cache) : base(action, options, cache)
        {

        }

        /// <inheritdoc/>
        public override DynamicParameters ToCreateParameters()
        {
            var parameters = base.ToCreateParameters();
            parameters.AddRecurringJobId(RecurringJobId, nameof(RecurringJobId));
            return parameters;
        }
    }
}
