using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// The current status of a <see cref="IDaemon"/>.
    /// </summary>
    public enum DaemonStatus
    {
        /// <summary>
        /// Daemon is stopped.
        /// </summary>
        Stopped = 0,
        /// <summary>
        /// Daemon is starting up.
        /// </summary>
        Starting = 1,
        /// <summary>
        /// Daemon is running.
        /// </summary>
        Running = 2,
        /// <summary>
        /// Daemon threw exception while running.
        /// </summary>
        Faulted = 3,
        /// <summary>
        /// Daemon gracefully executed while running.
        /// </summary>
        Finished = 3,
        /// <summary>
        /// Daemon is cancelling/stopping.
        /// </summary>
        Stopping = 5,

    }
}
