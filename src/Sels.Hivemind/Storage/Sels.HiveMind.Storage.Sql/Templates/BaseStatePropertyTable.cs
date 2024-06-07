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
        public override void AppendCreateParameters(DynamicParameters parameters, int index)
        {
            parameters.ValidateArgument(nameof(parameters));
            index.ValidateArgumentLargerOrEqual(nameof(index), 0);

            parameters.Add($"{nameof(StateId)}{index}", StateId, DbType.Int64, ParameterDirection.Input);
            base.AppendCreateParameters(parameters, index);
        }

        /// <inheritdoc />
        public override void AppendUpdateParameters(DynamicParameters parameters, int? index)
        {
            parameters.ValidateArgument(nameof(parameters));

            parameters.Add($"{nameof(StateId)}{index}", StateId, DbType.Int64, ParameterDirection.Input);
            base.AppendUpdateParameters(parameters, index);
        }
    }
}
