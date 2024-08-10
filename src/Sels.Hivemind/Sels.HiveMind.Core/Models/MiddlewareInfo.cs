using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <inheritdoc cref="IMiddlewareInfo"/>
    public class MiddlewareInfo : IMiddlewareInfo
    {
        // Fields
        private readonly object _lock = new object();
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache? _cache;

        // State
        private MiddlewareStorageData _data;
        private Type _type;
        private object _context;

        // Properties
        /// <inheritdoc/>
        public Type Type
        {
            get
            {
                lock (_lock)
                {
                    if (_type == null && _data != null && _data.TypeName != null)
                    {
                        _type = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.TypeName, typeof(Type), _options, _cache).CastTo<Type>();
                    }

                    return _type;
                }
            }
            protected set
            {
                lock (_lock)
                {
                    _type = value;
                    _data ??= new MiddlewareStorageData();
                    _data.TypeName = value != null ? HiveMindHelper.Storage.ConvertToStorageFormat(value, _options, _cache) : null;
                }
            }
        }
        /// <inheritdoc/>
        public object Context
        {
            get
            {
                lock (_lock)
                {
                    if( _context == null && _data != null && _data.ContextTypeName != null)
                    {
                        var type = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.ContextTypeName, typeof(Type), _options, _cache).CastTo<Type>();

                        _context = _data.Context != null ? HiveMindHelper.Storage.ConvertFromStorageFormat(_data.Context, type, _options, _cache) :  type.GetDefaultValue();
                    }

                    return _context;
                }
            }
            protected set
            {
                lock (_lock)
                {
                    _context = value;

                    _data ??= new MiddlewareStorageData();

                    if(value != null)
                    {
                        _data.ContextTypeName = HiveMindHelper.Storage.ConvertToStorageFormat(value.GetType(), _options, _cache);
                        _data.Context = HiveMindHelper.Storage.ConvertToStorageFormat(value, _options, _cache);
                    }
                }
            }
        }
        /// <inheritdoc/>
        public byte? Priority
        {
            get
            {
                lock (_lock)
                {
                    return _data?.Priority;
                }
            }
            protected set
            {
                lock (_lock)
                {
                    _data ??= new MiddlewareStorageData();
                    _data.Priority = value;
                }
            }
        }

        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public MiddlewareStorageData StorageData
        {
            get
            {
                lock (_lock)
                {
                    if (_data == null)
                    {
                        _data = new MiddlewareStorageData();
                    }
                    return _data;
                }
            }
        }

        /// <inheritdoc cref="MiddlewareInfo"/>
        /// <param name="type"><inheritdoc cref="Type"/></param>
        /// <param name="context"><inheritdoc cref="Context"/></param>
        /// <param name="priority"><inheritdoc cref="Priority"/></param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public MiddlewareInfo(Type type, object context, byte? priority, HiveMindOptions options, IMemoryCache? cache = null) : this(options, cache)
        {
            Type = type.ValidateArgument(nameof(type));
            Context = context;
            Priority = priority;
        }

        /// <inheritdoc cref="MiddlewareInfo"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public MiddlewareInfo(MiddlewareStorageData data, HiveMindOptions options, IMemoryCache? cache = null) : this(options, cache)
        {
            _data = data;
        }

        /// <inheritdoc cref="MiddlewareInfo"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public MiddlewareInfo(IMiddlewareStorageData data, HiveMindOptions options, IMemoryCache? cache = null) : this(options, cache)
        {
            _data = new MiddlewareStorageData()
            {
                TypeName = data.TypeName,
                ContextTypeName = data.ContextTypeName,
                Context = data.Context,
                Priority = data.Priority
            };
        }

        /// <summary>
        /// Ctor for derived classes.
        /// </summary>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        protected MiddlewareInfo(HiveMindOptions options, IMemoryCache? cache = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }

    }
}
