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
    /// Model that maps to the table that contains the properties linked to a daemon.
    /// </summary>
    public class ColonyDaemonPropertyTable : BasePropertyTable
    {
        // Properties
        /// <summary>
        /// The id of the colony the daemon is linked to.
        /// </summary>
        public string ColonyId { get; set; }
        /// <summary>
        /// The name of the daemon the property is linked to.
        /// </summary>
        public string DaemonName { get; set; }

        /// <inheritdoc cref="ColonyDaemonPropertyTable"/>
        public ColonyDaemonPropertyTable()
        {

        }

        /// <inheritdoc cref="ColonyDaemonPropertyTable"/>
        /// <param name="colonyId"><inheritdoc cref="ColonyId"/></param>
        /// <param name="daemonName"><inheritdoc cref="DaemonName"/></param>
        /// <param name="storageProperty">The instance to convert from</param>
        public ColonyDaemonPropertyTable(string colonyId, string daemonName, StorageProperty storageProperty) : base(storageProperty)
        {
            ColonyId = colonyId;
            DaemonName = daemonName;
        }

        /// <inheritdoc />
        public override void AppendCreateParameters(DynamicParameters parameters, string suffix)
        {
            parameters = Guard.IsNotNull(parameters);
            suffix = Guard.IsNotNullOrWhitespace(suffix);

            parameters.AddColonyId(ColonyId, $"{nameof(ColonyId)}{suffix}");
            parameters.AddDaemonName(DaemonName, $"{nameof(DaemonName)}{suffix}");
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
