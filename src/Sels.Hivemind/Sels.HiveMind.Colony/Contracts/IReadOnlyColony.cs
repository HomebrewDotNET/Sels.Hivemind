using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a colony of <see cref="IReadOnlyDaemon"/>(s) connected to a HiveMind environment.
    /// Cannot be modified.
    /// </summary>
    public interface IReadOnlyColony : IColonyInfo
    {
        // Properties
        /// <summary>
        /// The daemons managed by the colony.
        /// </summary>
        new IReadOnlyList<IReadOnlyDaemon> Daemons { get; }
        /// <summary>
        /// Object that can be used to synchronise access to <see cref="LocalProperties"/> and <see cref="IColonyInfo.Properties"/>.
        /// </summary>
        public object SyncRoot { get; }
        /// <summary>
        /// The in-memory properties assigned to this colony. Not persisted to storage.
        /// </summary>
        public IReadOnlyDictionary<string, object> LocalProperties { get; }
    }
}
