using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob
{
    /// <summary>
    /// The options used by a swarm host that processes background jobs.
    /// </summary>
    /// <typeparam name="TOptions">The type of inhereting this interface</typeparam>
    public interface IBackgroundJobSwarmHostOptions<TOptions> : ISwarmHostOptions<TOptions>
    {
        /// <summary>
        /// It's possible for new jobs to still be in a transaction when jobs are dequeued.
        /// Drones attempting to fetch the background job will receive a not found.
        /// If the enqueue date of the dequeued job is within the commit time the drone will try again instead of dropping the dequeued job.
        /// When set to null the default from <see cref="BackgroundJobSwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public TimeSpan? MaxJobCommitTime { get; }
        /// <summary>
        /// How long to wait on a commiting background job before delaying the dequeued job. Will only be performed when dequeued job is within <see cref="MaxJobCommitTime"/>.
        /// When set to null the default from <see cref="BackgroundJobSwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public TimeSpan? MaxNotFoundWaitTime { get; }
        /// <summary>
        /// How many times within <see cref="MaxNotFoundWaitTime"/> to check if the job was commited.
        /// When set to null the default from <see cref="BackgroundJobSwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public int? NotFoundCheckInterval { get; }

        /// <summary>
        /// How long to delay a dequeued job by if the background job is already locked.
        /// When set to null the default from <see cref="BackgroundJobSwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public TimeSpan? LockedDelay { get; }
        /// <summary>
        ///  How long before a background job lock is supposed to expire to set the heartbeat.
        ///  When set to null the default from <see cref="BackgroundJobSwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public TimeSpan? LockHeartbeatSafetyOffset { get; }
    }
}
