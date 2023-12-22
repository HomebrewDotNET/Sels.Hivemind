using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Exposes the current state of a drone.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the swarm</typeparam>
    public interface IDroneState<TOptions>
    {
        /// <summary>
        /// The state of the swarm the drone is attached to.
        /// </summary>
        public ISwarmState<TOptions> Swarm { get; }
        /// <summary>
        /// The name of the drone.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The full name of the drone including the swarm name.
        /// </summary>
        public string FullName => $"{Swarm.Name}.{Name}";

        /// <summary>
        /// True if the drone is currently processing, otherwise false if it's currently waiting for work.
        /// </summary>
        public bool IsProcessing { get; }
        /// <summary>
        /// True if the drone is currently working on a job that wwas assigned to it's swarm, otherwise false if working on a job from a parent swarm or if not processing.
        /// </summary>
        public bool IsWorkingOnDedicated { get; }

        /// <summary>
        /// The id of the job the drone is currently working on if <see cref="IsProcessing"/> is set to true.
        /// </summary>
        public string JobId { get; }
        /// <summary>
        /// The name of the queue of the job the drone is currently working on if <see cref="IsProcessing"/> is set to true.
        /// </summary>
        public string JobQueue { get; }
        /// <summary>
        /// The priority of the job the drone is currently working on if <see cref="IsProcessing"/> is set to true.
        /// </summary>
        public QueuePriority JobPriority { get; }
    }
}
