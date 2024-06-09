using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains the configured options for a <see cref="IColony"/>.
    /// </summary>
    public interface IColonyOptions
    {
        /// <summary>
        /// How often the colony should check it's daemons.
        /// </summary>
        public TimeSpan DaemonManagementInterval { get; } 
        /// <summary>
        /// The default enabled log level for logs created by running daemons.
        /// </summary>
        public LogLevel DefaultDaemonLogLevel { get;}
        /// <inheritdoc cref="HiveColonyCreationOptions"/>
        public HiveColonyCreationOptions CreationOptions { get; }
        /// <summary>
        /// How many times a <see cref="ScheduledDaemon"/> will attempt to generate the next schedule date. Used to avoid infinite loops when the schedule is invalid.
        /// </summary>
        public int MaxScheduleTries { get; }
    }
}
