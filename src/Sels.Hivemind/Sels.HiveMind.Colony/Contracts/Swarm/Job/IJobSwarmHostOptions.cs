using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Colony.Swarm;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// The options used by a swarm host that processes jobs.
    /// </summary>
    /// <typeparam name="TOptions">The type of inhereting this interface</typeparam>
    public interface IJobSwarmHostOptions<TOptions> : ISwarmHostOptions<TOptions> where TOptions : IJobSwarmHostOptions<TOptions>
    {
        /// <summary>
        /// It's possible for new jobs to still be in a transaction when jobs are dequeued.
        /// Drones attempting to fetch the background job will receive a not found exception.
        /// If the enqueue date of the dequeued job is within the commit time the drone will try again instead of dropping the dequeued job.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? MaxJobCommitTime { get; }
        /// <summary>
        /// How long to wait on a commiting background job before delaying the dequeued job. Will only be performed when dequeued job is within <see cref="MaxJobCommitTime"/>.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? MaxNotFoundWaitTime { get; }
        /// <summary>
        /// How many times within <see cref="MaxNotFoundWaitTime"/> to check if the job was commited.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public int? NotFoundCheckInterval { get; }

        /// <summary>
        /// How long to delay a dequeued job by if the job is already locked.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? LockedDelay { get; }
        /// <summary>
        /// How much time a drone has to save any pending state after it's been cancelled.
        /// Used to correctly set (error) state on the job and dequeued job.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? MaxSaveTime { get; }
    }
}
