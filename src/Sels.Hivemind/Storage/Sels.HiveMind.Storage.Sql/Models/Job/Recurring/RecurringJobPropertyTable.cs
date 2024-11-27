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
    public class RecurringJobPropertyTable : BasePropertyTable
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
        public DynamicParameters ToCreateParameters(string suffix)
        {
            suffix.ValidateArgument(nameof(suffix));

            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(RecurringJobId, $"{nameof(RecurringJobId)}{suffix}");
            AppendCreateParameters(parameters, suffix);
            return parameters;
        }

        /// <inheritdoc/>
        public override void AppendCreateParameters(DynamicParameters parameters, string suffix)
        {
            base.AppendCreateParameters(parameters, suffix);
            parameters.AddRecurringJobId(RecurringJobId, $"{nameof(RecurringJobId)}{suffix}");
        }

        /// <summary>
        /// Creates dapper parameters to update the current instance.
        /// </summary>
        /// <returns>Dapper parameters to update the current instance</returns>
        public DynamicParameters ToUpdateParameters(string suffix)
        {
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(RecurringJobId, suffix.HasValue() ? $"{nameof(RecurringJobId)}{suffix}" : nameof(RecurringJobId));
            AppendUpdateParameters(parameters, suffix);
            return parameters;
        }
        /// <inheritdoc/>
        public override void AppendUpdateParameters(DynamicParameters parameters, string suffix)
        {
            base.AppendUpdateParameters(parameters, suffix);
            parameters.AddRecurringJobId(RecurringJobId, suffix.HasValue() ? $"{nameof(RecurringJobId)}{suffix}" : nameof(RecurringJobId));
        }
    }
}
