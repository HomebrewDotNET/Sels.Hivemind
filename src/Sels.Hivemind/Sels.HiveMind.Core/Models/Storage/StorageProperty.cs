using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// A property transformed into a format for storage.
    /// </summary>
    public class StorageProperty
    {
        // Properties
        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The original .net type name of <see cref="StorageValue"/>.
        /// </summary>
        public string OriginalType { get; set; }
        /// <summary>
        /// How <see cref="StorageValue"/> was transformed.
        /// </summary>
        public StorageType StorageType { get; set; }
        /// <summary>
        /// The property value transformed into a format for storage. 
        /// </summary>
        public object StorageValue { get; set; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="value">The .net object to store</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public StorageProperty(string name, object value, HiveMindOptions options, IMemoryCache cache = null)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            options.ValidateArgument(nameof(options));
            
            if(value == null)
            {
                OriginalType = typeof(object).FullName;
                StorageType = StorageType.Json;
                StorageValue = value;
            }
            else
            {
                OriginalType = value.GetType().FullName;
                StorageType = HiveMindHelper.Storage.GetStorageType(value);
                StorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(StorageType, value, options, cache);
            }
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public StorageProperty()
        {
            
        }

        /// <summary>
        /// Returns <see cref="StorageValue"/> converted back into <see cref="OriginalType"/>.
        /// </summary>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <returns><see cref="StorageValue"/> converted back into <see cref="OriginalType"/></returns>
        public object GetValue(HiveMindOptions options, IMemoryCache cache = null)
        {
            OriginalType.ValidateArgument(nameof(OriginalType));
            options.ValidateArgument(nameof(options));

            var originalType = System.Type.GetType(OriginalType);
            if (StorageValue == null) return originalType.GetDefaultValue();

            return HiveMindHelper.Storage.ConvertFromStorageFormat(StorageType, StorageValue, originalType, options, cache);
        }
    }
}
