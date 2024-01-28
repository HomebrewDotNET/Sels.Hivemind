using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a process kept alive by a <see cref="IColony"/>.
    /// </summary>
    public interface IDaemon : IReadOnlyDaemon
    {
        // Properties
        /// <summary>
        /// The in-memory properties assigned to this daemon. Not persisted to storage.
        /// </summary>
        public new IDictionary<string, object> LocalProperties { get; }
        /// <summary>
        /// The properties assigned to this daemon. Can be queried.
        /// </summary>
        public new IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Signal the daemon to start running if it's not running.
        /// Method does not wait for it to start.
        /// </summary>
        public void Start();
        /// <summary>
        /// Signal the daemon to stop running if it's running.
        /// Method doesn't wait for it to stop.
        /// </summary>
        public void Cancel();
    }
}
