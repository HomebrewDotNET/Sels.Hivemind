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

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Represents a lazy loaded background job state.
    /// </summary>
    public class BackgroundJobStateInfo
    {
        // Fields
        private readonly Lazy<IBackgroundJobService> _lazyBackgroundJobService;
        private readonly object _lock = new object();
        private readonly HiveMindOptions _options;

        // State
        private IBackgroundJobState _state;
        private JobStateStorageData _data;

        // Properties
        /// <inheritdoc cref="IBackgroundJobState"/>
        public IBackgroundJobState State
        {
            get
            {
                lock (_lock)
                {
                    if(_state == null && _data != null && _data.OriginalTypeName != null)
                    {
                        _state = _lazyBackgroundJobService.Value.ConvertToState(_data, _options);
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
        public JobStateStorageData StorageData
        {
            get
            {
                lock (_lock)
                {
                    if (_data == null && _state != null)
                    {
                        var properties = _lazyBackgroundJobService.Value.GetStorageProperties(_state, _options);
                        _data = new JobStateStorageData(_state, properties);
                    }

                    return _data ?? new JobStateStorageData();
                }
            }
        }

        /// <inheritdoc cref="BackgroundJobStateInfo"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="lazyBackgroundJobService">Lazy that should returns a <see cref="IBackgroundJobService"/> that will be used to convert states</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public BackgroundJobStateInfo(JobStateStorageData data, Lazy<IBackgroundJobService> lazyBackgroundJobService, HiveMindOptions options) : this(lazyBackgroundJobService, options)
        {
            _data = data;
        }

        /// <inheritdoc cref="BackgroundJobStateInfo"/>
        /// <param name="state"><inheritdoc cref="State"/></param>
        /// <param name="lazyBackgroundJobService">Lazy that should returns a <see cref="IBackgroundJobService"/> that will be used to convert states</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public BackgroundJobStateInfo(IBackgroundJobState state, Lazy<IBackgroundJobService> lazyBackgroundJobService, HiveMindOptions options) : this(lazyBackgroundJobService, options)
        {
            State = state;
        }

        /// <inheritdoc cref="BackgroundJobStateInfo"/>
        /// <param name="lazyBackgroundJobService">Lazy that should returns a <see cref="IBackgroundJobService"/> that will be used to convert states</param>
        /// <param name="options">The configured options for the environment</param>
        public BackgroundJobStateInfo(Lazy<IBackgroundJobService> lazyBackgroundJobService, HiveMindOptions options)
        {
            _lazyBackgroundJobService = lazyBackgroundJobService.ValidateArgument(nameof(lazyBackgroundJobService));
            _options = options.ValidateArgument(nameof(options));
        }
    }
}
