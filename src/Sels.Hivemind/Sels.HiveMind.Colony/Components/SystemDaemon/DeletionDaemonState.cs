using Sels.HiveMind.Colony.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.SystemDaemon
{
    /// <summary>
    /// Contains the current state of a deletion daemon.
    /// </summary>
    public class DeletionDaemonState
    {
        /// <summary>
        /// How many jobs were deleted since the daemon started.
        /// </summary>
        public long Deleted { get; internal set; }
        /// <summary>
        /// How many jobs are currently being deleted.
        /// </summary>
        public int Deleting { get; internal set; }
        /// <summary>
        /// How many drones are currently actively deleting jobs.
        /// </summary>
        public int ActiveDrones { get; internal set; }
        /// <summary>
        /// The current scheduled state of the daemon.
        /// </summary>
        public ScheduledDaemonState DaemonState { get; internal set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Deleted: {Deleted} | Deleting: {Deleting} | Active Drones: {ActiveDrones} ({DaemonState})";
        }
    }
}
