using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Exposes the current state of a swarm.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the swarm</typeparam>
    public interface ISwarmState<out TOptions>
    {
        /// <summary>
        /// The options assigned to this swarm.
        /// </summary>
        public TOptions Options { get; }
        /// <summary>
        /// The name of the swarm.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The drones managed by the swarm.
        /// </summary>
        public IReadOnlyList<IDroneState<TOptions>>? Drones { get; }
        /// <summary>
        /// The parent swarm in case the current swarm isn't the root swarm.
        /// </summary>
        public ISwarmState<TOptions>? Parent { get; }
        /// <summary>
        /// Any child swarms defined for this swarm.
        /// </summary>
        public IReadOnlyList<ISwarmState<TOptions>>? ChildSwarms { get; }
        /// <summary>
        /// True if the swarm is the root swarm, otherwise false if it has a parent.
        /// </summary>
        public bool IsRootSwarm  => Parent != null;

        /// <summary>
        /// The sum of all jobs that were processed by the drones of the current swarm.
        /// </summary>
        public long Processed => Drones != null ? Drones.Sum(x => x.Processed) : 0;
    }
}
