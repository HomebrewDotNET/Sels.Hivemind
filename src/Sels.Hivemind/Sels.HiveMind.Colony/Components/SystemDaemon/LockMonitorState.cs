using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.SystemDaemon
{
    /// <summary>
    /// Contains the current state of a lock monitor daemon.
    /// </summary>
    public class LockMonitorState
    {
        /// <summary>
        /// How many timed out background jobs were released since the daemon started.
        /// </summary>
        public long ReleasedTimedOutBackgroundJobs { get; internal set; }
        /// <summary>
        /// How many timed out recurring jobs were released since the daemon started.
        /// </summary>
        public long ReleasedTimedOutRecurringJobs { get; internal set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Timed out recurring/background jobs handled: {ReleasedTimedOutRecurringJobs}/{ReleasedTimedOutBackgroundJobs}";
        }
    }
}
