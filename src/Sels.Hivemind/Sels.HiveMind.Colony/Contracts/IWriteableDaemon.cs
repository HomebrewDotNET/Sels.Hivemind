using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a process kept alive by a <see cref="IColony"/>.
    /// Can be modified.
    /// </summary>
    public interface IWriteableDaemon : IReadOnlyDaemon
    {
        /// <summary>
        /// The colony the daemon is running under.
        /// </summary>
        public new IWriteableColony Colony { get; }
        /// <summary>
        /// The in-memory properties assigned to this colony. Not persisted to storage.
        /// </summary>
        public new ConcurrentDictionary<string, object> LocalProperties { get; }
        /// <summary>
        /// The queryable properties of the colony. Peristed to storage on a schedule.
        /// </summary>
        public new ConcurrentDictionary<string, object> Properties { get; }
    }
}
