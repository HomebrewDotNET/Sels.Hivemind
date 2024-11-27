using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// The current status of a <see cref="IColonyInfo"/>.
    /// </summary>
    public enum ColonyStatus
    {
        /// <summary>
        /// Colony is not running.
        /// </summary>
        Stopped = 0,
        /// <summary>
        /// Colony is starting up.
        /// </summary>
        Starting = 1,
        /// <summary>
        /// Colony is waiting on it's process lock so it can start running.
        /// </summary>
        WaitingForLock = 2,
        /// <summary>
        /// Colony has acquired it's process lock and is starting it's daemons.
        /// </summary>
        StartingDaemons = 3,
        /// <summary>
        /// Colony is running.
        /// </summary>
        Running = 4,
        /// <summary>
        /// Colony is stopping.
        /// </summary>
        Stopping = 5,
        /// <summary>
        /// Colony (or it's daemons) failed to stop gracefully.
        /// </summary>
        FailedToStop = 6,
        /// <summary>
        /// Colony ran into an issue while starting up.
        /// </summary>
        FailedToStart = 7,
        /// <summary>
        /// Colony ran into an issue while trying to get it's process lock.
        /// </summary>
        FailedToLock = 8,
        /// <summary>
        /// Colony wasn't able to start any of it's daemons.
        /// </summary>
        FailedToStartDaemons = 9,
        /// <summary>
        /// Colony lost it's process lock while processing and is cancelling it's running daemons.
        /// </summary>
        LostLock = 10
    }
}
