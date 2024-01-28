using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Represents a queue that a swarm can work on.
    /// </summary>
    public interface ISwarmQueue
    {
        /// <summary>
        /// The name of the queue.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The priority of the queue in the swarm. Lower value means higher priority.
        /// </summary>
        public byte? Priority { get; }
    }
}
