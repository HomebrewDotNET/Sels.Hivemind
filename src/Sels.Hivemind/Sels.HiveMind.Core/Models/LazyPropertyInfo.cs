using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Represents a searchable property on something that is lazy loaded.
    /// </summary>
    public class LazyPropertyInfo
    {
        // Fields
        private readonly object _lock = new object();
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // State
        private object _value;
        private StorageProperty _data;

        // Properties
        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Name
        {
            get
            {
                lock (_lock)
                {
                    return _data?.Name;
                }
            }
            set
            {
                lock( _lock )
                {
                    _data ??= new StorageProperty();
                    _data.Name = value;
                }
            }
        }
        /// <summary>
        /// The value of the property.
        /// </summary>
        public object Value { 
            get { 
                lock(_lock)
                {
                    if(_value == null && _data != null && _data.StorageValue != null)
                    {
                        var type = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.OriginalTypeName, typeof(Type), _options, _cache).CastTo<Type>();
                        _value = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.StorageType, _data.StorageValue, type, _options, _cache);
                    }

                    return _value;
                }
            } 
            set
            {
                lock (_lock)
                {
                    _value = value;
                    _data ??= new StorageProperty();
                    if (_value != null)
                    {
                        _data.OriginalTypeName = value.GetType().AssemblyQualifiedName;
                        _data.StorageType = HiveMindHelper.Storage.GetStorageType(value);
                        _data.StorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(_data.StorageType, value, _options, _cache);

                        if (_data.StorageValue is string stringStorage && stringStorage.Length > HiveMindConstants.Storage.TextTypeMaxSize)
                        {
                            _data.StorageType = StorageType.Serialized;
                        }
                    }
                    else
                    {
                        _data.OriginalTypeName = typeof(object).AssemblyQualifiedName;
                        _data.StorageValue = null;
                        _data.StorageType = StorageType.Text;
                    }
                }
            } 
        }

        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public StorageProperty StorageData
        {
            get
            {
                lock (_lock)
                {
                    if (_data == null)
                    {
                        _data = new StorageProperty();
                    }
                    return _data;
                }
            }
        }

        /// <inheritdoc cref="LazyPropertyInfo"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public LazyPropertyInfo(StorageProperty data, HiveMindOptions options, IMemoryCache? cache = null) : this(options, cache)
        {
            _data = data;
        }

        /// <inheritdoc cref="LazyPropertyInfo"/>
        /// <param name="value"><inheritdoc cref="Value"/></param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public LazyPropertyInfo(object value, HiveMindOptions options, IMemoryCache? cache = null) : this(options, cache)
        {
            Value = value;
        }

        /// <inheritdoc cref="LazyPropertyInfo"/>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public LazyPropertyInfo(HiveMindOptions options, IMemoryCache? cache = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }
    }
}
