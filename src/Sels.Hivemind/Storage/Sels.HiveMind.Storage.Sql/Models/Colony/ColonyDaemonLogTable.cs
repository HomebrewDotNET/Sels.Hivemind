using Dapper;
using Sels.Core.Extensions;
using Sels.HiveMind.Job.Recurring;
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
    /// Model that maps to the table that contains the processing logs of a daemon.
    /// </summary>
    public class ColonyDaemonLogTable : LogEntry
    {
        // Properties
        /// <summary>
        /// The id of the colony the daemon is linked to.
        /// </summary>
        public string ColonyId { get; set; }
        /// <summary>
        /// The name of the daemon the log came from.
        /// </summary>
        public string Name { get; set; }

        /// <inheritdoc cref="ColonyDaemonLogTable"/>
        public ColonyDaemonLogTable()
        {
            
        }

        /// <inheritdoc cref="ColonyDaemonLogTable"/>
        /// <param name="colonyId"><inheritdoc cref="ColonyId"/></param>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="logEntry">The instance to convert from</param>
        public ColonyDaemonLogTable(string colonyId, string name, LogEntry logEntry) : base(logEntry)
        {
            ColonyId = colonyId;
            Name = name;
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

            parameters.AddColonyId(ColonyId, $"{nameof(ColonyId)}{index}");
            parameters.AddDaemonName(Name, $"{nameof(Name)}{index}");
            parameters.Add($"{nameof(LogLevel)}{index}", LogLevel, DbType.Int32, ParameterDirection.Input);
            parameters.Add($"{nameof(Message)}{index}", Message, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(ExceptionType)}{index}", ExceptionType, DbType.String, ParameterDirection.Input, 65535);
            parameters.Add($"{nameof(ExceptionMessage)}{index}", ExceptionMessage, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(ExceptionStackTrace)}{index}", ExceptionStackTrace, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(CreatedAt)}{index}", CreatedAtUtc, DbType.DateTime2, ParameterDirection.Input);
        }
    }
}
