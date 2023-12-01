using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// The current status of a <see cref="IColony"/>.
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
        /// Colony is running.
        /// </summary>
        Running = 2,
        /// <summary>
        /// Colony is stopping.
        /// </summary>
        Stopping = 3,
        /// <summary>
        /// Colony is in a faulted state.
        /// </summary>
        Faulted = 4
    }
}
