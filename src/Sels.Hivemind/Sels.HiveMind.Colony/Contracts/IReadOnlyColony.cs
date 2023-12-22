using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a colony of <see cref="IDaemon"/>(s) connected to a HiveMind environment.
    /// Cannot be modified.
    /// </summary>
    public interface IReadOnlyColony
    {
        // Properties
        /// <summary>
        /// The unique id of the colony.
        /// </summary>
        public Guid Id { get; }
        /// <summary>
        /// The name of the colony.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The HiveMind environment that the colony is connected to.
        /// </summary>
        public string Environment { get; }
        /// <summary>
        /// The current status of the colony.
        /// </summary>
        public ColonyStatus Status { get; }
        /// <summary>
        /// The daemons managed by the colony.
        /// </summary>
        IReadOnlyList<IReadOnlyDaemon> Daemons { get; }
    }
}
