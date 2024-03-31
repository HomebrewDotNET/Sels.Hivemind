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

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Represents a lazy loaded job state.
    /// </summary>
    /// <typeparam name="TService">The type of service used for lazy loading the state</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TStateStorageData">The type of storage data used to store the job states</typeparam>
    /// <typeparam name="TStorageData">The type of storage data used by the job</typeparam>
    public class JobStateInfo<TService, TStorageData, TState, TStateStorageData> 
        where TState : IJobState 
        where TStateStorageData : JobStateStorageData, new()
        where TStorageData : JobStorageData
        where TService : IJobService<TStorageData, TState, TStateStorageData>
    {
        // FieldsW
        private readonly Lazy<TService> _lazyJobService;
        private readonly object _lock = new object();
        private readonly string _environment;

        // State
        private TState _state;
        private TStateStorageData _data;

        // Properties
        /// <inheritdoc cref="IBackgroundJobState"/>
        public TState State
        {
            get
            {
                lock (_lock)
                {
                    if(_state == null && _data != null && _data.OriginalTypeName != null)
                    {
                        _state = _lazyJobService.Value.ConvertToState(_data, _environment);
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
                        var properties = _lazyJobService.Value.GetStorageProperties(_state, _environment);
                        _data = new TStateStorageData()
                        {
                            ElectedDateUtc = _state.ElectedDateUtc,
                            Name = _state.Name,
                            OriginalTypeName = _state.GetType().AssemblyQualifiedName,
                            Properties = properties?.ToList(),
                            Reason = _state.Reason,
                        };
                    }

                    return _data ?? new TStateStorageData();
                }
            }
        }

        /// <inheritdoc cref="JobStateInfo{TStorageData, TState, TStateStorageData}"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="lazyBackgroundJobService">Lazy that should returns a <see cref="IBackgroundJobService"/> that will be used to convert states</param>
        /// <param name="environment">The HiveMind environment <paramref name="data"/> is from</param>
        public JobStateInfo(TStateStorageData data, Lazy<TService> lazyBackgroundJobService, string environment) : this(lazyBackgroundJobService, environment)
        {
            _data = data;
        }

        /// <inheritdoc cref="JobStateInfo{TStorageData, TState, TStateStorageData}"/>
        /// <param name="state"><inheritdoc cref="State"/></param>
        /// <param name="lazyBackgroundJobService">Lazy that should returns a <see cref="IBackgroundJobService"/> that will be used to convert states</param>
        /// <param name="environment">The HiveMind environment <paramref name="state"/> is from</param>
        public JobStateInfo(TState state, Lazy<TService> lazyBackgroundJobService, string environment) : this(lazyBackgroundJobService, environment)
        {
            State = state;
        }

        /// <inheritdoc cref="JobStateInfo{TStorageData, TState, TStateStorageData}"/>
        /// <param name="lazyBackgroundJobService">Lazy that should returns a <see cref="IBackgroundJobService"/> that will be used to convert states</param>
        /// <param name="environment">The HiveMind environment the current info is configured for</param>
        private JobStateInfo(Lazy<TService> lazyBackgroundJobService, string environment)
        {
            _lazyJobService = lazyBackgroundJobService.ValidateArgument(nameof(lazyBackgroundJobService));
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
