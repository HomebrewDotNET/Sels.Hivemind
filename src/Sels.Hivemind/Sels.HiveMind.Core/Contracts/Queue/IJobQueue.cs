using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Queue that can be used to queue jobs and later fetch them for processing.
    /// </summary>
    public interface IJobQueue
    {
        /// <summary>
        /// Places job <paramref name="jobId"/> on <paramref name="queue"/> so it can be processed after <paramref name="queueTime"/>.
        /// </summary>
        /// <param name="queueType">The type of the queue to place the job in. Different queue types are used for different types of processing (Background job processing, background job cleanup, recurring job trigger, ...)</param>
        /// <param name="queue">The name of the queue to place the job in</param>
        /// <param name="jobId">The id of the job to enqueue (Recurring, background, ...)</param>
        /// <param name="queueTime">The date (in utc) after which the job can be dequeued</param>
        /// <param name="executionId">Execution id to correlate the queue message and the state of the enqueued job. Can be used to detect changes between when the job was enqueued and the job was modified</param>
        /// <param name="priority">The priority of the job in <paramref name="queue"/></param>
        /// <param name="connection">Optional storage connection to execute the request with. Should not be required to function</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task EnqueueAsync(string queueType, string queue, string jobId, DateTime queueTime, Guid executionId, QueuePriority priority, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Places job <paramref name="jobId"/> on <paramref name="queue"/> so it can be processed after <paramref name="queueTime"/>.
        /// </summary>
        /// <param name="queueType">The type of the queue to place the job in. Different queue types are used for different types of processing (Background job processing, background job cleanup, recurring job trigger, ...)</param>
        /// <param name="queue">The name of the queue to place the job in</param>
        /// <param name="jobId">The id of the job to enqueue (Recurring, background, ...)</param>
        /// <param name="queueTime">The date (in utc) after which the job can be dequeued</param>
        /// <param name="executionId">Execution id to correlate the queue message and the state of the enqueued job. Can be used to detect changes between when the job was enqueued and the job was modified</param>
        /// <param name="priority">The priority of the job in <paramref name="queue"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task EnqueueAsync(string queueType, string queue, string jobId, DateTime queueTime, Guid executionId, QueuePriority priority, CancellationToken token = default)
            => EnqueueAsync(queueType, queue, jobId, queueTime, executionId, priority, token);
    }
}
