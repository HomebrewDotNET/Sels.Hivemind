using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Colony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage.Colony
{
    /// <summary>
    /// Contains the state of a colony transformed into a format for storage.
    /// </summary>
    public class ColonyStorageData
    {
        // Properties
        /// <inheritdoc cref="IColonyInfo.Id"/>
        [Traceable(HiveLog.Colony.Id)]
        public string Id { get; set; }
        /// <inheritdoc cref="IColonyInfo.Name"/>
        [Traceable(HiveLog.Colony.Name)]
        public string Name { get; set; }
        /// <inheritdoc cref="IColonyInfo.Environment"/>
        [Traceable(HiveLog.Environment)]
        public string Environment { get; set; }
        /// <summary>
        /// The current state of the lock on the colony transformed into a format for storage.
        /// </summary>
        public LockStorageData LockStorageData { get; set; }
        /// <inheritdoc cref="IColonyInfo.Status"/>
        [Traceable(HiveLog.Colony.Status)]
        public ColonyStatus Status { get; set; }
        /// <inheritdoc cref="IColonyInfo.Id"/>
        public ColonyOptions Options { get; set; }
        /// <summary>
        /// The state of the daemons managed by the colony transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<DaemonStorageData> Daemons { get; set; }
        /// <summary>
        /// The queryable properties of the colony transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<StorageProperty> Properties { get; set; }

        /// <inheritdoc cref="ColonyStorageData"/>
        public ColonyStorageData()
        {
            
        }

        /// <inheritdoc cref="ColonyStorageData"/>
        /// <param name="colony">The instance to convert from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public ColonyStorageData(IColonyInfo colony, HiveMindOptions options, IMemoryCache? cache = null)
        {
            colony = Guard.IsNotNull(colony);
            options = Guard.IsNotNull(options);

            Id = colony.Id;
            Name = colony.Name;
            Environment = colony.Environment;
            if(colony.Lock != null) LockStorageData = new LockStorageData(colony.Lock);
            Status = colony.Status;
            Options = colony.Options != null && colony.Options.IsAssignableTo<ColonyOptions>() ? colony.Options.CastTo<ColonyOptions>() : colony.Options != null ? new ColonyOptions(colony.Options) : new ColonyOptions();
            if(colony.Daemons.HasValue()) Daemons = colony.Daemons.Select(x => new DaemonStorageData(x, options, cache)).ToList();
            if (colony.Properties.HasValue()) Properties = colony.Properties.Select(x => new StorageProperty(x.Key, x.Value, options, cache)).ToList();
        }
    }
}
