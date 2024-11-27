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
        /// Daemon gracefully executed while running.
        /// </summary>
        Finished = 3,
        /// <summary>
        /// Daemon is cancelling/stopping.
        /// </summary>
        Stopping = 4,
        /// <summary>
        /// Daemon threw exception while running.
        /// </summary>
        Faulted = 5,
        /// <summary>
        /// Daemon threw exception while it was requested to stop.
        /// </summary>
        FailedToStop = 6,
        /// <summary>
        /// Daemon ran into an issue while starting up.
        /// </summary>
        FailedToStart = 7,
        /// <summary>
        /// Daemon did not stop within the configured timeout.
        /// This state can be considered the corrupted state since the task of the daemon could be doing anything. Restart not possible until task stops.
        /// </summary>
        Timedout = 8,
    }
}
