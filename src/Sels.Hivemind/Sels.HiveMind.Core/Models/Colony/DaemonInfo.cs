using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Storage.Colony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IDaemonInfo"/>
    public class DaemonInfo : IDaemonInfo
    {
        // Fields
        private readonly LazyPropertyInfoDictionary _lazyProperties;
        private readonly string? _originalInstanceTypeName;
        private readonly string? _originalStateTypeName;
        private readonly string? _stateData;
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache? _cache;

        // State
        private object? _state;
        private Type? _instanceType;

        // Properties
        /// <inheritdoc/>
        public IColonyInfo Colony { get; }
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public byte Priority { get; }
        /// <inheritdoc/>
        public Type? InstanceType { get 
            {
                if (_instanceType == null && _originalInstanceTypeName.HasValue())
                {
                    _instanceType = HiveMindHelper.Storage.ConvertFromStorageFormat(_originalInstanceTypeName!, typeof(Type), _options, _cache).CastTo<Type>();
                }

                return _instanceType;
            } 
        }
        /// <inheritdoc/>
        public DaemonStatus Status { get; }
        /// <inheritdoc/>
        public object? State { get 
            {
                if (_state == null && _originalInstanceTypeName.HasValue() && _stateData.HasValue())
                {
                    var stateType = HiveMindHelper.Storage.ConvertFromStorageFormat(_originalInstanceTypeName!, typeof(Type), _options, _cache).CastTo<Type>();
                    _state = HiveMindHelper.Storage.ConvertFromStorageFormat(_stateData!, stateType, _options, _cache);
                }

                return _state;
            } 
        }
        /// <inheritdoc/>
        public DaemonRestartPolicy RestartPolicy { get; }
        /// <inheritdoc/>
        public LogLevel EnabledLogLevel { get; }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties { get; }

        /// <inheritdoc cref="DaemonInfo"/>
        /// <param name="">The instance to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public DaemonInfo(IColonyInfo colony, DaemonStorageData data, HiveMindOptions options, IMemoryCache? cache = null)
        {
            colony = Guard.IsNotNull(colony);
            data = Guard.IsNotNull(data);
            _options = Guard.IsNotNull(options);
            _cache = cache;

            Colony = colony;
            Name = data.Name;
            Priority = data.Priority;
            _originalInstanceTypeName = data.OriginalInstanceTypeName;
            _originalStateTypeName = data.StateTypeName;
            _stateData = data.StateStorageValue;
            Status = data.Status;
            RestartPolicy = data.RestartPolicy;
            EnabledLogLevel = data.EnabledLogLevel;

            _lazyProperties = new LazyPropertyInfoDictionary(data.Properties.HasValue() ? data.Properties.Select(x => new LazyPropertyInfo(x, options, cache)) : Array.Empty<LazyPropertyInfo>(), options, cache);
        }
    }
}
