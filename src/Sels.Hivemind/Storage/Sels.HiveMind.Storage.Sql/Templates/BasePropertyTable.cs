using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class for a table that contains queryable properties on an object.
    /// </summary>
    public class BasePropertyTable : BaseTable
    {
        /// <inheritdoc cref="StorageProperty.Name"/>
        public string? Name { get; set; }
        /// <inheritdoc cref="StorageProperty.StorageType"/>
        public StorageType Type { get; set; }
        /// <inheritdoc cref="StorageProperty.OriginalTypeName"/>
        public string? OriginalType { get; set; }
        /// <summary>
        /// Columns set when <see cref="Type"/> is set to <see cref="StorageType.Text"/>.
        /// </summary>
        public string? TextValue { get; set; }
        /// <summary>
        /// Columns set when <see cref="Type"/> is set to <see cref="StorageType.Number"/>.
        /// </summary>
        public long? NumberValue { get; set; }
        /// <summary>
        /// Columns set when <see cref="Type"/> is set to <see cref="StorageType.FloatingNumber"/>.
        /// </summary>
        public double? FloatingNumberValue { get; set; }
        /// <summary>
        /// Columns set when <see cref="Type"/> is set to <see cref="StorageType.Date"/>.
        /// </summary>
        public DateTime? DateValue { get; set; }
        /// <summary>
        /// Columns set when <see cref="Type"/> is set to <see cref="StorageType.Serialized"/>.
        /// </summary>
        public string? OtherValue { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public BasePropertyTable(StorageProperty property)
        {
            property.ValidateArgument(nameof(property));
            
            Name = property.Name;
            Type = property.StorageType;
            OriginalType = property.OriginalTypeName;

            switch (Type)
            {
                case StorageType.Number:
                    NumberValue = property.StorageValue.CastToOrDefault<long?>();
                    break;
                case StorageType.FloatingNumber:
                    FloatingNumberValue = property.StorageValue.CastToOrDefault<double?>();
                    break;
                case StorageType.Text:
                    TextValue = property.StorageValue.CastToOrDefault<string>();
                    break;
                case StorageType.Date:
                    DateValue = property.StorageValue.CastToOrDefault<DateTime?>();
                    break;
                case StorageType.Serialized:
                    OtherValue = property.StorageValue.CastToOrDefault<string>();
                    break;
                default:
                    throw new NotSupportedException($"Storage type <{Type}> is not supported");
            }
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BasePropertyTable()
        {

        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public StorageProperty ToStorageFormat()
        {
            var storageFormat = new StorageProperty()
            {
                Name = Name,
                OriginalTypeName = OriginalType,
                StorageType = Type
            };

            switch (Type)
            {
                case StorageType.Number:
                    storageFormat.StorageValue = NumberValue;
                    break;
                case StorageType.FloatingNumber:
                    storageFormat.StorageValue = FloatingNumberValue;
                    break;
                case StorageType.Text:
                    storageFormat.StorageValue = TextValue;
                    break;
                case StorageType.Date:
                    storageFormat.StorageValue = DateValue;
                    break;
                case StorageType.Serialized:
                    storageFormat.StorageValue = OtherValue;
                    break;
                default:
                    throw new NotSupportedException($"Storage type <{Type}> is not supported");
            }

            return storageFormat;
        }
    }
}
