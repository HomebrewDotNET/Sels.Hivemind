using Sels.Core.Tracing;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Storage.Sql.Templates;
using Sels.HiveMind.Storage.Colony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sels.Core;
using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Job.Background;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;

namespace Sels.HiveMind.Storage.Sql.Models.Colony
{
    /// <summary>
    /// Model that maps to the table that contains the state of a daemon connected to a colony.
    /// </summary>
    public class ColonyDaemonTable : BaseTable
    {
        // Properties
        /// <summary>
        /// The colony id the daemon is linked to.
        /// </summary>
        public string ColonyId { get; set; }
        /// <inheritdoc cref="DaemonStorageData.Name"/>
        [Traceable(HiveLog.Daemon.Name)]
        public string Name { get; set; }
        /// <inheritdoc cref="DaemonStorageData.Priority"/>
        [Traceable(HiveLog.Daemon.Priority)]
        public byte Priority { get; set; }
        /// <inheritdoc cref="DaemonStorageData.OriginalInstanceTypeName"/>
        public string? OriginalInstanceTypeName { get; set; }
        /// <inheritdoc cref="DaemonStorageData.Status"/>
        [Traceable(HiveLog.Daemon.Status)]
        public DaemonStatus Status { get; }
        /// <inheritdoc cref="DaemonStorageData.StateTypeName"/>
        public string? StateTypeName { get; set; }
        /// <inheritdoc cref="DaemonStorageData.StateStorageValue"/>
        public string? StateStorageValue { get; set; }
        /// <inheritdoc cref="DaemonStorageData.RestartPolicy"/>
        public DaemonRestartPolicy RestartPolicy { get; set; }
        /// <inheritdoc cref="DaemonStorageData.EnabledLogLevel"/>
        public LogLevel EnabledLogLevel { get; set; }

        /// <inheritdoc cref="ColonyDaemonTable"/>
        public ColonyDaemonTable()
        {
            
        }

        /// <inheritdoc cref="ColonyDaemonTable"/>
        /// <param name="data">The instance to convert from</param>
        public ColonyDaemonTable(string colonyId, DaemonStorageData data)
        {
            data = Guard.IsNotNull(data);

            ColonyId = colonyId;
            Name = data.Name;
            Priority = data.Priority;
            OriginalInstanceTypeName = data.OriginalInstanceTypeName;
            Status = data.Status;
            StateTypeName = data.StateTypeName;
            StateStorageValue = data.StateStorageValue;
            RestartPolicy = data.RestartPolicy;
            EnabledLogLevel = data.EnabledLogLevel;
        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public DaemonStorageData ToStorageFormat(HiveMindOptions options, IMemoryCache cache)
        {
            options.ValidateArgument(nameof(options));

            return new DaemonStorageData()
            {
                Name = Name,
                Priority = Priority,
                OriginalInstanceTypeName = OriginalInstanceTypeName,
                Status = Status,
                StateTypeName = StateTypeName,
                StateStorageValue = StateStorageValue,
                RestartPolicy = RestartPolicy,
                EnabledLogLevel = EnabledLogLevel
            };
        }

        /// <summary>
        /// Appends the sync parameters to <paramref name="parameters"/> of the current instance.
        /// </summary>
        /// <param name="parameters">The parameters bag to add the parameters in</param>
        public void AppendSyncParameters(DynamicParameters parameters, string suffix)
        {
            parameters = Guard.IsNotNull(parameters);
            suffix = Guard.IsNotNullOrWhitespace(suffix);

            parameters.AddColonyId(ColonyId, $"{nameof(ColonyId)}{suffix}");
            parameters.AddDaemonName(Name, $"{nameof(Name)}{suffix}");
            parameters.Add($"{nameof(Status)}{suffix}", Status, DbType.Int32);
            parameters.Add($"{nameof(Priority)}{suffix}", Priority, DbType.Byte, ParameterDirection.Input);
            parameters.Add($"{nameof(OriginalInstanceTypeName)}{suffix}", OriginalInstanceTypeName, DbType.String, size: 255);
            parameters.Add($"{nameof(StateTypeName)}{suffix}", StateTypeName, DbType.String, size: 255);
            parameters.Add($"{nameof(StateStorageValue)}{suffix}", StateStorageValue, DbType.String, ParameterDirection.Input, 16777215);
            parameters.Add($"{nameof(RestartPolicy)}{suffix}", RestartPolicy, DbType.Int32);
            parameters.Add($"{nameof(EnabledLogLevel)}{suffix}", EnabledLogLevel, DbType.Int32);
        }
    }
}
