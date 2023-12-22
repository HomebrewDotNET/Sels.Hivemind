using Microsoft.Extensions.Logging;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.Worker
{
    /// <summary>
    /// The options used by a worker swarm host.
    /// </summary>
    public interface IWorkerSwarmHostOptions : ISwarmHostOptions<WorkerSwarmHostOptions>
    {
        /// <summary>
        /// The middleware to use for jobs executed by this swarm.
        /// </summary>
        public IReadOnlyCollection<MiddlewareStorageData> Middleware { get; }
        /// <summary>
        /// If <see cref="Middleware"/> defined on parent swarms should be used.
        /// Default is true.
        /// </summary>
        public bool UseMiddlewareFromParentSwarms { get; }
        /// <summary>
        /// It's possible for new jobs to still be in a transaction when jobs are dequeued.
        /// Drones attempting to fetch the background job will receive a not found.
        /// If the enqueue date of the dequeued job is within the commit time the drone will try again instead of dropping the dequeued job.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public TimeSpan? MaxJobCommitTime { get; }
        /// <summary>
        /// How long to wait on a commiting background job before delaying the dequeued job. Will only be performed when dequeued job is within <see cref="MaxJobCommitTime"/>.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public TimeSpan? MaxNotFoundWaitTime { get; }
        /// <summary>
        /// How many times within <see cref="MaxNotFoundWaitTime"/> to check if the job was commited.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public int? NotFoundCheckInterval { get; }

        /// <summary>
        /// How long to delay a dequeued job by if the background job is already locked.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public TimeSpan? LockedDelay { get; }
        /// <summary>
        ///  How long before a background job lock is supposed to expire to set the heartbeat.
        ///  When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public TimeSpan? LockHeartbeatSafetyOffset { get; }
        /// <summary>
        /// The log level above which to persist logs created by executing background jobs.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public LogLevel? LogLevel { get; }
    }
}
