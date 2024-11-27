using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Storage.Colony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IColonyInfo"/>
    public class ColonyInfo : IColonyInfo, ILockInfo
    {
        // Fields
        private readonly LazyPropertyInfoDictionary _lazyProperties;

        // Properties
        /// <inheritdoc/>
        public string Id { get; }
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public string Environment { get; }
        /// <inheritdoc/>
        public ILockInfo Lock => LockedBy.HasValue() ? this : null!;
        /// <inheritdoc/>
        public ColonyStatus Status { get; }
        /// <inheritdoc/>
        public IColonyOptions Options { get; }
        /// <inheritdoc/>
        public IReadOnlyList<IDaemonInfo> Daemons { get; }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties { get; }
        /// <inheritdoc/>
        public string LockedBy { get; }
        /// <inheritdoc/>
        public DateTime LockedAtUtc { get; }
        /// <inheritdoc/>
        public DateTime LockHeartbeatUtc { get; }

        /// <inheritdoc cref="ColonyInfo"/>
        /// <param name="">The instance to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ColonyInfo(ColonyStorageData data, HiveMindOptions options, IMemoryCache? cache = null)
        {
            data = Guard.IsNotNull(data);
            options = Guard.IsNotNull(options);

            Id = data.Id;
            Name = data.Name;
            Environment = data.Environment;
            LockedBy = data.LockStorageData?.LockedBy!;
            LockedAtUtc = data.LockStorageData?.LockedAtUtc ?? DateTime.MinValue;
            LockHeartbeatUtc = data.LockStorageData?.LockHeartbeatUtc ?? DateTime.MinValue;
            Status = data.Status;
            Options = data.Options;
            Daemons = data.Daemons.HasValue() ? data.Daemons.Select(x => new DaemonInfo(this, x, options, cache)).ToList() : new List<DaemonInfo>();

            _lazyProperties = new LazyPropertyInfoDictionary(data.Properties.HasValue() ? data.Properties.Select(x => new LazyPropertyInfo(x, options, cache)) : Array.Empty<LazyPropertyInfo>(), options, cache);
        }
    }
}
