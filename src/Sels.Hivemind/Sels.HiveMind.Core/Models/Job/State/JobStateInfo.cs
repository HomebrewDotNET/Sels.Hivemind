using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Sels.Core.Extensions;
using Sels.HiveMind.Service;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using Sels.Core.Extensions.Conversion;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Represents a lazy loaded job state.
    /// </summary>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TStateStorageData">The type of storage data used to store the job states</typeparam>
    /// <typeparam name="TStorageData">The type of storage data used by the job</typeparam>
    public class JobStateInfo<TStorageData, TState, TStateStorageData> 
        where TState : IJobState 
        where TStateStorageData : JobStateStorageData, new()
        where TStorageData : JobStorageData
    {
        // Fields
        private readonly object _lock = new object();
        private readonly string _environment;
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // State
        private TState _state;
        private TStateStorageData _data;

        // Properties
        /// <inheritdoc cref="TState"/>
        public TState State
        {
            get
            {
                lock (_lock)
                {
                    if(_state == null && _data != null && _data.OriginalTypeName != null)
                    {
                        var type = Type.GetType(_data.OriginalTypeName, true);
                        _state = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.Data, type, _options, _cache).CastTo<TState>();
                        _state.Sequence = _data.Sequence;
                        _state.ElectedDateUtc = _data.ElectedDateUtc;
                        _state.Reason = _data.Reason;
                    }

                    return _state;
                }
            }
            set
            {
                lock (_lock)
                {
                    _state = value;
                    _data = null;
                }
            }
        }

        /// <summary>
        /// True if <see cref="State"/> is initialed, otherwise false.
        /// </summary>
        public bool IsInitialized => _state != null;

        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public TStateStorageData StorageData
        {
            get
            {
                lock (_lock)
                {
                    if (_data == null && _state != null)
                    {
                        _data = new TStateStorageData()
                        {
                            ElectedDateUtc = _state.ElectedDateUtc,
                            Name = _state.Name,
                            OriginalTypeName = _state.GetType().AssemblyQualifiedName,
                            Sequence = _state.Sequence,
                            Reason = _state.Reason,
                            Data = HiveMindHelper.Storage.ConvertToStorageFormat(_state, _options, _cache)
                        };
                    }

                    return _data ?? new TStateStorageData();
                }
            }
        }

        /// <inheritdoc cref="JobStateInfo{TService, TStorageData, TState, TStateStorageData}"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="options">The options to use for the configuration</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <param name="environment">The HiveMind environment <paramref name="data"/> is from</param>
        public JobStateInfo(TStateStorageData data, string environment, HiveMindOptions options, IMemoryCache cache = null) : this(environment, options, cache)
        {
            _data = data;
        }

        /// <inheritdoc cref="JobStateInfo{TService, TStorageData, TState, TStateStorageData}"/>
        /// <param name="state"><inheritdoc cref="State"/></param>
        /// <param name="options">The options to use for the configuration</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <param name="environment">The HiveMind environment <paramref name="state"/> is from</param>
        public JobStateInfo(TState state, string environment, HiveMindOptions options, IMemoryCache cache = null) : this(environment, options, cache)
        {
            State = state;
        }

        /// <inheritdoc cref="JobStateInfo{TService, TStorageData, TState, TStateStorageData}"/>
        /// <param name="options">The options to use for the configuration</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <param name="environment">The HiveMind environment the current info is configured for</param>
        private JobStateInfo(string environment, HiveMindOptions options, IMemoryCache cache = null)
        {
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }
    }
}
