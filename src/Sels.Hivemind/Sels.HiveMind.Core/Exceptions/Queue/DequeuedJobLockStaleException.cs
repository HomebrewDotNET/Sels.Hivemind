using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Thrown when a lock on a dequeued job becomes stale.
    /// </summary>
    public class DequeuedJobLockStaleException : Exception
    {
        // Properties
        /// <summary>
        /// The id of the dequeued job.
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// The queue the job was retrieved from.
        /// </summary>
        public string Queue { get; }
        /// <summary>
        /// The type of queue the job was retrieved from.
        /// </summary>
        public string QueueType { get; }
        /// <summary>
        /// The HiveMind environment the job was retrieved from.
        /// </summary>
        public string Environment { get; }

        /// <inheritdoc cref="DequeuedJobLockStaleException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="queue"><inheritdoc cref="Queue"/></param>
        /// <param name="queueType"><inheritdoc cref="QueueType"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        public DequeuedJobLockStaleException(string id, string queue, string queueType, string environment) : base($"Lock on dequeued job <{id}> retrieved fronm queue <{queue}> of type <{queueType}> in environment <{environment}> has become stale")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Queue = queue.ValidateArgumentNotNullOrWhitespace(nameof(queue));
            QueueType = queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
