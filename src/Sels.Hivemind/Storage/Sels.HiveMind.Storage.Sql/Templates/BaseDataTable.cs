using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class for tables that contains unqueryable data attached to jobs.
    /// </summary>
    public abstract class BaseDataTable
    {
        /// <summary>
        /// The name of the data entry.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The value serialized in a format for storage.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Creates dapper parameters to insert the current instance.
        /// </summary>
        /// <returns>Dapper parameters to insert the current instance</returns>
        public virtual DynamicParameters ToCreateParameters()
        {
            var parameters = new DynamicParameters();
            parameters.AddDataName(Name, nameof(Name));
            parameters.Add(nameof(Value), Value, DbType.String, ParameterDirection.Input, -1);

            return parameters;
        }
    }
}
