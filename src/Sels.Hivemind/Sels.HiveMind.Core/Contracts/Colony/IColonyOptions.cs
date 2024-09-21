using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains the configured options for a <see cref="IColonyInfo"/>.
    /// </summary>
    public interface IColonyOptions
    {
        /// <summary>
        /// How long the colony will wait before retrying a failure.
        /// </summary>
        public TimeSpan ErrorRetryDelay { get; }
        /// <summary>
        /// How long a colony has to try and release it's process lock before forcefully cancelling.
        /// </summary>
        public TimeSpan ReleaseLockTime { get; }
        /// <summary>
        /// How often the colony should check it's daemons (Start, restart, remove, ...)
        /// </summary>
        public TimeSpan DaemonManagementInterval { get; }
        /// <summary>
        /// How long a daemon has after sending the cancellation signal to finish stop it's task before it is considered <see cref="DaemonStatus.Timedout"/>.
        /// </summary>
        public TimeSpan DaemonMaxStopTime { get; }
        /// <summary>
        /// The default enabled log level for logs created by running daemons.
        /// </summary>
        public LogLevel DefaultDaemonLogLevel { get;}
        /// <inheritdoc cref="ColonyCreationOptions"/>
        public ColonyCreationOptions CreationOptions { get; }
        /// <summary>
        /// How often a colony will try to persist it's current state (and that of it's daemons including any new logs) to the storage.
        /// </summary>
        public TimeSpan StateSyncInterval { get; }
        /// <summary>
        /// How many times a scheduled daemon will attempt to generate the next schedule date. Used to avoid infinite loops when the schedule is invalid.
        /// </summary>
        public int MaxScheduleTries { get; }
    }
}
