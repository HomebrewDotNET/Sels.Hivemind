using Dapper;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the properties linked to a recurring job.
    /// </summary>
    public class RecurringJobPropertyTable : BaseStatePropertyTable
    {
        /// <summary>
        /// The id of the recurring job the property is linked to.
        /// </summary>
        public string RecurringJobId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public RecurringJobPropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public RecurringJobPropertyTable()
        {

        }

        /// <summary>
        /// Creates dapper parameters to insert the current instance.
        /// </summary>
        /// <returns>Dapper parameters to insert the current instance</returns>
        public DynamicParameters ToCreateParameters(int index)
        {
            index.ValidateArgumentLargerOrEqual(nameof(index), 0);

            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(RecurringJobId, $"{nameof(RecurringJobId)}{index}");
            AppendCreateParameters(parameters, index);
            return parameters;
        }

        /// <inheritdoc/>
        public override void AppendCreateParameters(DynamicParameters parameters, int index)
        {
            base.AppendCreateParameters(parameters, index);
            parameters.AddRecurringJobId(RecurringJobId, $"{nameof(RecurringJobId)}{index}");
        }

        /// <summary>
        /// Creates dapper parameters to update the current instance.
        /// </summary>
        /// <returns>Dapper parameters to update the current instance</returns>
        public DynamicParameters ToUpdateParameters(int? index)
        {
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(RecurringJobId, index.HasValue ? $"{nameof(RecurringJobId)}{index}" : nameof(RecurringJobId));
            AppendUpdateParameters(parameters, index);
            return parameters;
        }
        /// <inheritdoc/>
        public override void AppendUpdateParameters(DynamicParameters parameters, int? index)
        {
            base.AppendUpdateParameters(parameters, index);
            parameters.AddRecurringJobId(RecurringJobId, index.HasValue ? $"{nameof(RecurringJobId)}{index}" : nameof(RecurringJobId));
        }
    }
}
