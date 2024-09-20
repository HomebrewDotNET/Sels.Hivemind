using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
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
    }
}
