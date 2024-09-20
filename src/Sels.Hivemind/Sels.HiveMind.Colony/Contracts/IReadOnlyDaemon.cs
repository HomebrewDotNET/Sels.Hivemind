using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a process kept alive by a <see cref="IReadOnlyColony"/>.
    /// Cannot be modified.
    /// </summary>
    public interface IReadOnlyDaemon : IDaemonInfo
    {
        // Properties
        /// <summary>
        /// The colony the daemon is running under.
        /// </summary>
        public new IReadOnlyColony Colony { get; }
        /// <summary>
        /// If this daemon should be started automatically when the colony starts or when it is added to a running colony.
        /// </summary>
        public bool AutoStart { get; }
        /// <summary>
        /// The instance being executed by the daemon. Can be be null if the daemon is executing an anonymous delegate or if the daemon is not executing.
        /// </summary>
        public object Instance { get; }

        /// <summary>
        /// Object that can be used to synchronise access to <see cref="LocalProperties"/> and <see cref="IDaemonInfo.Properties"/>.
        /// </summary>
        public object SyncRoot { get; }

        /// <summary>
        /// The in-memory properties assigned to this daemon. Not persisted to storage.
        /// </summary>
        public IReadOnlyDictionary<string, object> LocalProperties { get; }
    }
}
