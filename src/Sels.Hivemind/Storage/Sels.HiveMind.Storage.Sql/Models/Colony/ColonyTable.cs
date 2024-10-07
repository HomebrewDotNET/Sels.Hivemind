using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Sels.Core;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions.DateTimes;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Storage.Colony;
using Sels.HiveMind.Storage.Job.Recurring;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage.Sql.Colony
{
    /// <summary>
    /// Model that maps to a table that contains colony data.
    /// </summary>
    public class ColonyTable : BaseLockableTable<string>
    {
        // Properties
        /// <inheritdoc cref="IColonyInfo.Name"/>
        public string? Name { get; set; }
        /// <inheritdoc cref="IColonyInfo.Status"/>
        public ColonyStatus Status { get; set; }
        /// <inheritdoc cref="IColonyInfo.Options"/>
        public string? Options { get; set; }

        /// <inheritdoc cref="ColonyTable"/>
        public ColonyTable()
        {
            
        }
        /// <summary>
        /// Creates an instance from <paramref name="colony"/>.
        /// </summary>
        /// <param name="colony">The instance to create from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public ColonyTable(ColonyStorageData colony, HiveMindOptions options, IMemoryCache? cache = null) : base(colony)
        {
            colony = Guard.IsNotNull(colony);
            options = Guard.IsNotNull(options);

            Name = colony.Name;
            Status = colony.Status;
            Options = colony.Options != null ? HiveMindHelper.Storage.ConvertToStorageFormat(colony.Options, options, cache) : null;
        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public ColonyStorageData ToStorageFormat(HiveMindOptions options, IMemoryCache? cache)
        {
            options = Guard.IsNotNull(options);

            return new ColonyStorageData()
            {
                Id = Id,
                Name = Name!,
                Status = Status,
                Options = Options != null ? HiveMindHelper.Storage.ConvertFromStorageFormat(Options, typeof(ColonyOptions), options, cache).ConvertTo<ColonyOptions>() : null!,
            };
        }
        /// <summary>
        /// Appends the sync parameters to <paramref name="parameters"/> of the current instance.
        /// </summary>
        /// <param name="parameters">The parameters bag to add the parameters in</param>
        public void AppendSyncParameters(DynamicParameters parameters)
        {
            parameters = Guard.IsNotNull(parameters);
            parameters.AddColonyId(Id, nameof(Id));
            parameters.Add(nameof(Name), Name, DbType.String, size: 255);
            parameters.Add(nameof(Status), Status, DbType.Int32);
            parameters.Add(nameof(Options), Options, DbType.String, ParameterDirection.Input, 16777215);
        }
    }
}
