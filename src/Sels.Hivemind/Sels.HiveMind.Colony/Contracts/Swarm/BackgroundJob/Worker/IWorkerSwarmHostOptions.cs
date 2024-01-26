using Microsoft.Extensions.Logging;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker
{
    /// <summary>
    /// The options used by a worker swarm host.
    /// </summary>
    public interface IWorkerSwarmHostOptions : IBackgroundJobSwarmHostOptions<WorkerSwarmHostOptions>
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
        /// The log level above which to persist logs created by executing background jobs.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public LogLevel? LogLevel { get; }
        /// <summary>
        /// How often logs for running background jobs will be flushed to storage.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public TimeSpan? LogFlushInterval { get; }

        /// <summary>
        /// How often to check for pending actions on executing background jobs.
        /// This is mainly used to cancel running jobs.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public TimeSpan? ActionPollingInterval { get; }
        /// <summary>
        /// How many actions can be pulled into memory at the same time.
        /// When set to null the default from <see cref="WorkerSwarmDefaultHostOptions"/> will be used.
        /// </summary>
        public int? ActionFetchLimit { get; }
    }
}
