using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// A ditionary consisting of <see cref="LazyPropertyInfo"/>(s).
    /// </summary>
    public class LazyPropertyInfoDictionary : IDictionary<string, LazyPropertyInfo>, IDictionary<string, object>, IReadOnlyDictionary<string, object>
    {
        // Fields
        private readonly List<LazyPropertyInfo> _properties;
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // Properties
        /// <summary>
        /// The properties contained in the dictionary.
        /// </summary>
        public IReadOnlyList<LazyPropertyInfo> Properties => _properties;
        /// <inheritdoc/>
        public ICollection<string> Keys => _properties.Select(x => x.Name).ToList();
        /// <inheritdoc/>
        public ICollection<LazyPropertyInfo> Values => _properties;
        /// <inheritdoc/>
        public int Count => _properties.Count;
        /// <inheritdoc/>
        public bool IsReadOnly => false;
        /// <inheritdoc/>
        ICollection<string> IDictionary<string, object>.Keys => Keys;
        /// <inheritdoc/>
        ICollection<object> IDictionary<string, object>.Values => Values.Select(x => x.Value).ToList();
        /// <inheritdoc/>
        int ICollection<KeyValuePair<string, object>>.Count => Count;
        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => IsReadOnly;
        /// <inheritdoc/>
        IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => Keys;
        /// <inheritdoc/>
        IEnumerable<object> IReadOnlyDictionary<string, object>.Values => Values.Select(x => x.Value).ToList();
        /// <inheritdoc/>
        int IReadOnlyCollection<KeyValuePair<string, object>>.Count => Count;
        /// <inheritdoc/>
        object IReadOnlyDictionary<string, object>.this[string key] => this[key]?.Value;

        /// <inheritdoc/>
        object IDictionary<string, object>.this[string key]
        {
            get
            {
                key.ValidateArgument(nameof(key));
                return (_properties.FirstOrDefault(x => key.EqualsNoCase(x.Name)) ?? throw new KeyNotFoundException($"Could not find property with name <{key}>")).Value;
            }
            set
            {
                value.ValidateArgument(nameof(value));
                var existing = _properties.FirstOrDefault(x => key.EqualsNoCase(x.Name)) ?? throw new KeyNotFoundException($"Could not find property with name <{key}>");
                existing.Value = value;
            }
        }

        /// <inheritdoc/>
        public LazyPropertyInfo this[string key] { 
            get {
                key.ValidateArgument(nameof(key));
                return _properties.FirstOrDefault(x => key.EqualsNoCase(x.Name)) ?? throw new KeyNotFoundException($"Could not find property with name <{key}>");
            }
            set {
                value.ValidateArgument(nameof(value));
                var existing = _properties.FirstOrDefault(x => key.EqualsNoCase(x.Name)) ?? throw new KeyNotFoundException($"Could not find property with name <{key}>");
                _properties.Remove(existing);
                _properties.Add(value);
            } 
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="properties">The initial items for the dictionary</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public LazyPropertyInfoDictionary(IEnumerable<LazyPropertyInfo> properties, HiveMindOptions options, IMemoryCache? cache = null) : this(options, cache)
        {
            _properties = new List<LazyPropertyInfo>(properties);
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public LazyPropertyInfoDictionary(HiveMindOptions options, IMemoryCache? cache = null)
        {
            _properties = new List<LazyPropertyInfo>();
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }

        /// <inheritdoc/>
        public void Add(string key, LazyPropertyInfo value)
        {
            key.ValidateArgument(nameof(key));
            value.ValidateArgument(nameof(value));

            value.Name = key;
            _properties.Add(value);
        }
        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            key.ValidateArgument(nameof(key));
            return _properties.FirstOrDefault(x => key.EqualsNoCase(x.Name)) != null;
        }
        /// <inheritdoc/>
        public bool Remove(string key)
        {
            key.ValidateArgument(nameof(key));
            var existing = _properties.FirstOrDefault(x => key.EqualsNoCase(x.Name));
            if(existing != null)
            {
                _properties.Remove(existing);
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <inheritdoc/>
        public bool TryGetValue(string key, out LazyPropertyInfo value)
        {
            key.ValidateArgument(nameof(key));
            value = null;

            if (ContainsKey(key))
            {
                value = this[key];
                return true;
            }
            return false;
        }
        /// <inheritdoc/>
        public void Add(KeyValuePair<string, LazyPropertyInfo> item) => Add(item.Key, item.Value);
        /// <inheritdoc/>
        public void Clear()
        {
            _properties.Clear();
        }
        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, LazyPropertyInfo> item)
        {
            if (ContainsKey(item.Key))
            {
                var value = this[item.Key];

                return value.Equals(item.Value);
            }

            return false;
        }
        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, LazyPropertyInfo>[] array, int arrayIndex)
        {
            var propertyArray = new LazyPropertyInfo[array.Length];

            _properties.CopyTo(propertyArray, arrayIndex);

            for(int i = arrayIndex; i < propertyArray.Length; i++)
            {
                array[i] = new KeyValuePair<string, LazyPropertyInfo>(propertyArray[i].Name, propertyArray[i]);
            }
        }
        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, LazyPropertyInfo> item) => Remove(item.Key);
        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, LazyPropertyInfo>> GetEnumerator() => _properties.Select(x => new KeyValuePair<string, LazyPropertyInfo>(x.Name, x)).GetEnumerator();
        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        /// <inheritdoc/>
        void IDictionary<string, object>.Add(string key, object value) => Add(key, new LazyPropertyInfo(value, _options, _cache));
        /// <inheritdoc/>
        bool IDictionary<string, object>.ContainsKey(string key) => ContainsKey(key);
        /// <inheritdoc/>
        bool IDictionary<string, object>.Remove(string key) => Remove(key);
        /// <inheritdoc/>
        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            key.ValidateArgument(nameof(key));
            value = null;

            if(TryGetValue(key, out var property))
            {
                value = property.Value;
                return true;
            }

            return false;
        }
        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item) => Add(new KeyValuePair<string, LazyPropertyInfo>(item.Key, new LazyPropertyInfo(item.Value, _options, _cache)));
        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, object>>.Clear() => Clear();
        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item) => Contains(new KeyValuePair<string, LazyPropertyInfo>(item.Key, new LazyPropertyInfo(item.Value, _options, _cache)));
        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            var propertyArray = new LazyPropertyInfo[array.Length];

            _properties.CopyTo(propertyArray, arrayIndex);

            for (int i = arrayIndex; i < propertyArray.Length; i++)
            {
                array[i] = new KeyValuePair<string, object>(propertyArray[i].Name, propertyArray[i].Value);
            }
        }
        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item) => Remove(new KeyValuePair<string, LazyPropertyInfo>(item.Key, new LazyPropertyInfo(item.Value, _options, _cache)));
        /// <inheritdoc/>
        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() => _properties.Select(x => new KeyValuePair<string, object>(x.Name, x.Value)).GetEnumerator();
        /// <inheritdoc/>
        bool IReadOnlyDictionary<string, object>.ContainsKey(string key) => ContainsKey(key);
        /// <inheritdoc/>
        bool IReadOnlyDictionary<string, object>.TryGetValue(string key, out object value)
        {
            key.ValidateArgument(nameof(key));
            value = null;

            if (TryGetValue(key, out var property))
            {
                value = property.Value;
                return true;
            }

            return false;
        }
    }
}
