using Dapper;
using Sels.Core;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage.Sql.Models.Colony
{
    /// <summary>
    /// Model that maps to the table that contains the properties linked to a colony.
    /// </summary>
    public class ColonyPropertyTable : BasePropertyTable
    {
        // Properties
        /// <summary>
        /// The id of the colony the property is linked to.
        /// </summary>
        public string ColonyId { get; set; }

        /// <inheritdoc cref="ColonyPropertyTable"/>
        public ColonyPropertyTable()
        {
            
        }

        /// <inheritdoc cref="ColonyPropertyTable"/>
        /// <param name="colonyId"><inheritdoc cref="ColonyId"/></param>
        public ColonyPropertyTable(string colonyId, StorageProperty storageProperty) : base(storageProperty)
        {
            ColonyId = colonyId;
        }

        /// <inheritdoc />
        public override void AppendCreateParameters(DynamicParameters parameters, string suffix)
        {
            parameters = Guard.IsNotNull(parameters);
            suffix = Guard.IsNotNullOrWhitespace(suffix);

            base.AppendCreateParameters(parameters, suffix);
            parameters.AddColonyId(ColonyId, $"{nameof(ColonyId)}{suffix}");
            parameters.AddPropertyName(Name, $"{nameof(Name)}{suffix}");
            parameters.Add($"{nameof(Type)}{suffix}", Type, DbType.Int32, ParameterDirection.Input);
            parameters.Add($"{nameof(OriginalType)}{suffix}", OriginalType, DbType.String, ParameterDirection.Input, 65535);
            parameters.Add($"{nameof(TextValue)}{suffix}", TextValue, DbType.String, ParameterDirection.Input, 255);
            parameters.Add($"{nameof(NumberValue)}{suffix}", NumberValue, DbType.Int64, ParameterDirection.Input);
            parameters.Add($"{nameof(FloatingNumberValue)}{suffix}", FloatingNumberValue, DbType.Double, ParameterDirection.Input);
            parameters.Add($"{nameof(DateValue)}{suffix}", DateValue, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add($"{nameof(BooleanValue)}{suffix}", BooleanValue, DbType.Boolean, ParameterDirection.Input);
            parameters.Add($"{nameof(OtherValue)}{suffix}", OtherValue, DbType.String, ParameterDirection.Input, 16777215);
        }
    }
}
