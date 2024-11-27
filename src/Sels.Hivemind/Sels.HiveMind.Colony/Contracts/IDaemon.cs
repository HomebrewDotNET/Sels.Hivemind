using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a process kept alive by a <see cref="IColony"/>.
    /// </summary>
    public interface IDaemon : IWriteableDaemon
    {
        // Properties
        /// <summary>
        /// The colony the daemon is running under.
        /// </summary>
        public new IColony Colony { get; }
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
