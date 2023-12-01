using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a process kept alive by a <see cref="IColony"/>.
    /// Cannot be modified.
    /// </summary>
    public interface IReadOnlyDaemon
    {
        // Properties
        /// <summary>
        /// The unique name of the daemon within the same colony.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The start/stop priority of the daemon. Lower value means start earlier and stopped later.
        /// </summary>
        public ushort Priority { get; }
        /// <summary>
        /// The instance being executed by the daemon. Can be be null if the daemon is executing an anonymous delegate or if the daemon is not executing.
        /// </summary>
        public object Instance { get; }
        /// <summary>
        /// The type of the instance that the daemon can execute. Can be be null if the daemon is executing an anonymous delegate.
        /// </summary>
        public Type InstanceType { get; }
        /// <summary>
        /// The current status of the daemon.
        /// </summary>
        public DaemonStatus Status { get; }
        /// <summary>
        /// Optional state that a daemon can expose. 
        /// </summary>
        public object State { get; }
        /// <summary>
        /// The restart policy for this daemon.
        /// </summary>
        public DaemonRestartPolicy RestartPolicy { get; }

        /// <summary>
        /// Object that can be used to synchronise access to <see cref="LocalProperties"/> and <see cref="Properties"/>.
        /// </summary>
        public object SyncRoot { get; }

        /// <summary>
        /// The in-memory properties assigned to this daemon. Not persisted to storage.
        /// </summary>
        public IReadOnlyDictionary<string, object> LocalProperties { get; }
        /// <summary>
        /// The properties assigned to this daemon. Can be queried.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}
