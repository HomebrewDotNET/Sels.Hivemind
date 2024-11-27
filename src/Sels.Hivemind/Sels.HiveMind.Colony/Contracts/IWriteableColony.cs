using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a colony of <see cref="IDaemon"/>(s) connected to a HiveMind environment.
    /// Can be modified.
    /// </summary>
    public interface IWriteableColony : IReadOnlyColony
    {
        /// <summary>
        /// The in-memory properties assigned to this colony. Not persisted to storage.
        /// </summary>
        public new ConcurrentDictionary<string, object> LocalProperties { get; }
        /// <summary>
        /// The queryable properties of the colony. Peristed to storage on a schedule.
        /// </summary>
        public new ConcurrentDictionary<string, object> Properties { get; }
        /// <summary>
        /// The daemons managed by the colony.
        /// </summary>
        new IReadOnlyList<IWriteableDaemon> Daemons { get; }

        /// <summary>
        /// Changes the configuration of the current colony.
        /// </summary>
        /// <param name="configure">Delegate used to configure the current instance</param>
        public void Configure(Action<IColonyConfigurator> configure);
    }
}
