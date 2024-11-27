using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Colony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage.Colony
{
    /// <summary>
    /// The state of a daemon tranformed into a format for storage.
    /// </summary>
    public class DaemonStorageData
    {
        // Properties
        /// <inheritdoc cref="IDaemonInfo.Name"/>
        [Traceable(HiveLog.Daemon.Name)]
        public string Name { get; set; }
        /// <inheritdoc cref="IDaemonInfo.Priority"/>
        [Traceable(HiveLog.Daemon.Priority)]
        public byte Priority { get; set; }
        /// <summary>
        /// The original .net type name of the instance that the daemon can execute. Can be be null if the daemon is executing an anonymous delegate.
        /// </summary>
        public string? OriginalInstanceTypeName { get; set; }
        /// <inheritdoc cref="IDaemonInfo.Status"/>
        [Traceable(HiveLog.Daemon.Status)]
        public DaemonStatus Status { get; set; }
        /// <summary>
        /// The original .net type name of <see cref="StateStorageValue"/>.
        /// </summary>
        public string? StateTypeName { get; set; }
        /// <summary>
        /// <see cref="IDaemonInfo.State"/> transformed into a format for storage when exposed by the daemon.
        /// </summary>
        public string? StateStorageValue { get; set; }
        /// <summary>
        /// The restart policy for this daemon.
        /// </summary>
        public DaemonRestartPolicy RestartPolicy { get; set; }
        /// <summary>
        /// The log level above which to start persisted created logs.
        /// </summary>
        public LogLevel EnabledLogLevel { get; set; }
        /// <summary>
        /// Any new log entries to persist for the daemon.
        /// </summary>
        public IReadOnlyList<LogEntry> NewLogEntries { get; set; }

        /// <summary>
        /// The queryable properties of the daemon transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<StorageProperty> Properties { get; set; }

        /// <inheritdoc cref="DaemonStorageData"/>
        public DaemonStorageData()
        {
            
        }

        /// <inheritdoc cref="DaemonStorageData"/>
        /// <param name="daemon">The instance to convert from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public DaemonStorageData(IDaemonInfo daemon, HiveMindOptions options, IMemoryCache? cache = null)
        {
            daemon = Guard.IsNotNull(daemon);
            options = Guard.IsNotNull(options);

            Name = daemon.Name;
            Priority = daemon.Priority;
            OriginalInstanceTypeName = daemon.InstanceType != null ? HiveMindHelper.Storage.ConvertToStorageFormat(daemon.InstanceType, options, cache) : null;
            Status = daemon.Status;
            if(daemon.State != null && daemon.State.GetType().IsPublic)
            {
                StateTypeName = HiveMindHelper.Storage.ConvertToStorageFormat(daemon.State.GetType(), options, cache);
                StateStorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(daemon.State, options, cache);
            }
            RestartPolicy = daemon.RestartPolicy;
            EnabledLogLevel = daemon.EnabledLogLevel;

            if(daemon.Properties.HasValue()) Properties = daemon.Properties.Select(x => new StorageProperty(x.Key, x.Value, options, cache)).ToList();
        }
    }
}
