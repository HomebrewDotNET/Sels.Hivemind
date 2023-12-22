using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using System.Reflection;
using System.Text;

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
        public string OriginalTypeName { get; set; }
        /// <summary>
        /// How <see cref="StorageValue"/> was transformed.
        /// </summary>
        public StorageType StorageType { get; set; } = StorageType.Text;
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
                OriginalTypeName = typeof(object).AssemblyQualifiedName;
                StorageType = StorageType.Serialized;
                StorageValue = value;
            }
            else
            {
                OriginalTypeName = value.GetType().AssemblyQualifiedName;
                StorageType = HiveMindHelper.Storage.GetStorageType(value);
                StorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(StorageType, value, options, cache);
            }

            if(StorageValue is string stringStorage && stringStorage.Length > HiveMindConstants.Storage.TextTypeMaxSize)
            {
               StorageType = StorageType.Serialized;
            }
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public StorageProperty()
        {
            
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(OriginalTypeName).AppendSpace();
            builder.Append(Name).AppendSpace().Append('=').AppendSpace();

            builder.Append('(').Append(StorageType).Append(')').AppendSpace();

            if(StorageValue != null)
            {
                if (StorageType.In(StorageType.Date, StorageType.Text, StorageType.Date))
                {
                    builder.Append('"').Append(StorageValue).Append('"').AppendSpace();
                }
                else
                {
                    builder.Append(StorageValue);
                }
            }
            else
            {
                builder.Append("NULL");
            }

            return builder.ToString();
        }
    }
}
