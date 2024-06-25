using Dapper;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// base class for a table that contains queryable properties on a state of a job.
    /// </summary>
    public class BaseStatePropertyTable : BasePropertyTable
    {
        /// <summary>
        /// The id of the state the property is linked to.
        /// </summary>
        public long StateId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public BaseStatePropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseStatePropertyTable()
        {

        }

        /// <inheritdoc />
        public override void AppendCreateParameters(DynamicParameters parameters, string suffix)
        {
            parameters.ValidateArgument(nameof(parameters));
            suffix.ValidateArgument(nameof(suffix));

            parameters.Add($"{nameof(StateId)}{suffix}", StateId, DbType.Int64, ParameterDirection.Input);
            base.AppendCreateParameters(parameters, suffix);
        }

        /// <inheritdoc />
        public override void AppendUpdateParameters(DynamicParameters parameters, string suffix)
        {
            parameters.ValidateArgument(nameof(parameters));

            parameters.Add($"{nameof(StateId)}{suffix}", StateId, DbType.Int64, ParameterDirection.Input);
            base.AppendUpdateParameters(parameters, suffix);
        }
    }
}
