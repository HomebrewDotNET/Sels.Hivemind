using Dapper;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to the table that contains the properties linked to a background job.
    /// </summary>
    public class BackgroundJobPropertyTable : BasePropertyTable
    {
        /// <summary>
        /// The id of the background job the property is linked to.
        /// </summary>
        public long BackgroundJobId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public BackgroundJobPropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BackgroundJobPropertyTable()
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
            parameters.AddBackgroundJobId(BackgroundJobId, $"{nameof(BackgroundJobId)}{index}");
            AppendCreateParameters(parameters, index);
            return parameters;
        }

        /// <inheritdoc/>
        public override void AppendCreateParameters(DynamicParameters parameters, int index)
        {
            base.AppendCreateParameters(parameters, index);
            parameters.AddBackgroundJobId(BackgroundJobId, $"{nameof(BackgroundJobId)}{index}");
        }

        /// <summary>
        /// Creates dapper parameters to update the current instance.
        /// </summary>
        /// <returns>Dapper parameters to update the current instance</returns>
        public DynamicParameters ToUpdateParameters(int? index)
        {
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(BackgroundJobId, index.HasValue ? $"{nameof(BackgroundJobId)}{index}" : nameof(BackgroundJobId));
            AppendUpdateParameters(parameters, index);
            return parameters;
        }
        /// <inheritdoc/>
        public override void AppendUpdateParameters(DynamicParameters parameters, int? index)
        {
            base.AppendUpdateParameters(parameters, index);
            parameters.AddBackgroundJobId(BackgroundJobId, index.HasValue ? $"{nameof(BackgroundJobId)}{index}" : nameof(BackgroundJobId));
        }
    }
}
