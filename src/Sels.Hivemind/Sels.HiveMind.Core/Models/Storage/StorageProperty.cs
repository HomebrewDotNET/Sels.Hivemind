using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// A property transformed into a format for storage.
    /// </summary>
    public class StorageProperty
    {
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
        public StorageProperty(string name, object value)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            
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
                StorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(StorageType, value);
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
        /// <returns><see cref="StorageValue"/> converted back into <see cref="OriginalType"/></returns>
        public object GetValue()
        {
            OriginalType.ValidateArgument(nameof(OriginalType));

            var originalType = System.Type.GetType(OriginalType);
            if (StorageValue == null) return originalType.GetDefaultValue();

            return HiveMindHelper.Storage.ConvertFromStorageFormat(StorageType, StorageType, originalType);
        }
    }
}
