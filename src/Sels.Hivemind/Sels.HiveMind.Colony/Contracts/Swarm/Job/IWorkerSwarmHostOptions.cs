using Microsoft.Extensions.Logging;
using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// The options used by a worker swarm host that processes jobs by executing them.
    /// </summary>
    /// <typeparam name="TMiddleware">The interface type of the middleware used when executing jobs</typeparam>
    /// <typeparam name="TOptions">The type inhereting this interface</typeparam>
    public interface IWorkerSwarmHostOptions<TMiddleware, TOptions> : IJobSwarmHostOptions<TOptions> 
        where TOptions : IWorkerSwarmHostOptions<TMiddleware, TOptions>
        where TMiddleware : class
    {
        /// <summary>
        /// List with the <see cref="TMiddleware"/>(s) to use when executing jobs. Can be empty.
        /// </summary>
        public IReadOnlyList<ISwarmHostMiddlewareOptions<TMiddleware>>? JobMiddleware { get; }

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
