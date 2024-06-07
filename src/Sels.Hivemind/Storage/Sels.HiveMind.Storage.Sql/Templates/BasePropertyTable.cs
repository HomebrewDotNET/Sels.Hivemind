using Dapper;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Data;
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
        /// Columns set when <see cref="Type"/> is set to <see cref="StorageType.Bool"/>.
        /// </summary>
        public bool? BooleanValue { get; set; }
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
                case StorageType.Bool:
                    BooleanValue = property.StorageValue.CastToOrDefault<bool?>();
                    break;
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
                case StorageType.Bool:
                    storageFormat.StorageValue = BooleanValue;
                    break;
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

        /// <summary>
        /// Appends the create parameters to <paramref name="parameters"/> to insert the current instance.
        /// </summary>
        /// <param name="parameters">The parameters bag to add the insert parameters in</param>
        /// <param name="index">Unique index for the current number. Used as a suffix for the parameter names</param>
        public virtual void AppendCreateParameters(DynamicParameters parameters, int index)
        {
            parameters.ValidateArgument(nameof(parameters));
            index.ValidateArgumentLargerOrEqual(nameof(index), 0);

            parameters.AddPropertyName(Name, $"{nameof(Name)}{index}");
            parameters.Add($"{nameof(Type)}{index}", Type, DbType.Int32, ParameterDirection.Input);
            parameters.Add($"{nameof(OriginalType)}{index}", OriginalType, DbType.String, ParameterDirection.Input, 65535);
            parameters.Add($"{nameof(TextValue)}{index}", TextValue, DbType.String, ParameterDirection.Input, 255);
            parameters.Add($"{nameof(NumberValue)}{index}", NumberValue, DbType.Int64, ParameterDirection.Input);
            parameters.Add($"{nameof(FloatingNumberValue)}{index}", FloatingNumberValue, DbType.Double, ParameterDirection.Input);
            parameters.Add($"{nameof(DateValue)}{index}", DateValue, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add($"{nameof(BooleanValue)}{index}", BooleanValue, DbType.Boolean, ParameterDirection.Input);
            parameters.Add($"{nameof(OtherValue)}{index}", OtherValue, DbType.String, ParameterDirection.Input, 16777215);
            parameters.Add($"{nameof(CreatedAt)}{index}", CreatedAt, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add($"{nameof(ModifiedAt)}{index}", ModifiedAt, DbType.DateTime2, ParameterDirection.Input);
        }

        /// <summary>
        /// Appends the update parameters to <paramref name="parameters"/> to update the current instance.
        /// </summary>
        /// <param name="parameters">The parameters bag to add the insert parameters in</param>
        /// <param name="index">Unique index for the current number. Used as a suffix for the parameter names</param>
        public virtual void AppendUpdateParameters(DynamicParameters parameters, int? index)
        {
            parameters.ValidateArgument(nameof(parameters));

            parameters.AddPropertyName(Name, index.HasValue ? $"{nameof(Name)}{index}" : nameof(Name));
            parameters.Add(index.HasValue ? $"{nameof(Type)}{index}" : nameof(Type), Type, DbType.Int32, ParameterDirection.Input);
            parameters.Add(index.HasValue ? $"{nameof(OriginalType)}{index}" : nameof(OriginalType), OriginalType, DbType.String, ParameterDirection.Input, 65535);
            parameters.Add(index.HasValue ? $"{nameof(TextValue)}{index}" : nameof(TextValue), TextValue, DbType.String, ParameterDirection.Input, 255);
            parameters.Add(index.HasValue ? $"{nameof(NumberValue)}{index}" : nameof(NumberValue), NumberValue, DbType.Int64, ParameterDirection.Input);
            parameters.Add(index.HasValue ? $"{nameof(FloatingNumberValue)}{index}" : nameof(FloatingNumberValue), FloatingNumberValue, DbType.Double, ParameterDirection.Input);
            parameters.Add(index.HasValue ? $"{nameof(DateValue)}{index}" : nameof(DateValue), DateValue, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add(index.HasValue ? $"{nameof(BooleanValue)}{index}" : nameof(BooleanValue), BooleanValue, DbType.Boolean, ParameterDirection.Input);
            parameters.Add(index.HasValue ? $"{nameof(OtherValue)}{index}" : nameof(OtherValue), OtherValue, DbType.String, ParameterDirection.Input, 16777215);
            parameters.Add(index.HasValue ? $"{nameof(ModifiedAt)}{index}" : nameof(ModifiedAt), ModifiedAt, DbType.DateTime2, ParameterDirection.Input);
        }
    }
}
