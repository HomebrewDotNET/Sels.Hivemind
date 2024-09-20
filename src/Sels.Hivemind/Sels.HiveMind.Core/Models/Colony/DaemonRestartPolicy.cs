using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Determines the restart policy when daemons stop running.
    /// </summary>
    public enum DaemonRestartPolicy
    {
        /// <summary>
        /// Do nothing when daemon changes status.
        /// </summary>
        None = 0,
        /// <summary>
        /// Restart daemon unless status is <see cref="DaemonStatus.Stopped"/>.
        /// </summary>
        UnlessStopped = 1,
        /// <summary>
        /// Always start daemon regardless if it crashed, finished executing, was stopped, ...
        /// </summary>
        Always = 2,
        /// <summary>
        /// Restart daemon only when when it's status is any of the faulted statuses.
        /// </summary>
        OnFailure = 3
    }
}
