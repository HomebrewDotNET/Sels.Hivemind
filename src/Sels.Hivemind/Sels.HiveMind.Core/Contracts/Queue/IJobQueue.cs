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
        /// <inheritdoc cref="JobQueueFeatures"/>
        JobQueueFeatures Features { get; }

        /// <summary>
        /// Places job <paramref name="jobId"/> on <paramref name="queue"/> so it can be processed after <paramref name="queueTime"/>.
        /// </summary>
        /// <param name="queueType">The type of the queue to place the job in. Different queue types are used for different types of processing (Background job processing, background job cleanup, recurring job trigger, ...)</param>
        /// <param name="queue">The name of the queue to place the job in</param>
        /// <param name="jobId">The id of the job to enqueue (Recurring, background, ...)</param>
        /// <param name="queueTime">The date (in utc) after which the job can be dequeued</param>
        /// <param name="executionId">Execution id to correlate the queue message and the state of the enqueued job. Can be used to detect changes between when the job was enqueued and the job was modified</param>
        /// <param name="priority">The priority of the job in <paramref name="queue"/></param>
        /// <param name="connection">Connection/transaction that can be used to execute the request. Useful when queue and storage share the same database. Should be an optional parameter and method should work even if connection is null</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task EnqueueAsync(string queueType, string queue, string jobId, DateTime queueTime, Guid executionId, QueuePriority priority, IStorageConnection connection, CancellationToken token = default);

        /// <summary>
        /// Returns the amount of jobs in <paramref name="queue"/> of type <paramref name="queueType"/>.
        /// </summary>
        /// <param name="queueType">The type of <paramref name="queue"/></param>
        /// <param name="queue">The queue to get the count for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The estimated amount of jobs in <paramref name="queue"/></returns>
        Task<long> GetQueueLengthAsync(string queueType, string queue, CancellationToken token = default);
        /// <summary>
        /// Returns the amount of background jobs in <paramref name="queue"/> that still need to be processed.
        /// </summary>
        /// <param name="queue">The queue to get the count for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The estimated amount of jobs in <paramref name="queue"/></returns>
        Task<long> GetBackgroundJobProcessQueueLengthAsync(string queue, CancellationToken token = default) => GetQueueLengthAsync(HiveMindConstants.Queue.BackgroundJobProcessQueueType, queue, token);
        /// <summary>
        /// Returns the amount of background jobs in <paramref name="queue"/> that still need to be cleaned up.
        /// </summary>
        /// <param name="queue">The queue to get the count for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The estimated amount of jobs in <paramref name="queue"/></returns>
        Task<long> GetBackgroundJobCleanupQueueLengthAsync(string queue, CancellationToken token = default) => GetQueueLengthAsync(HiveMindConstants.Queue.BackgroundJobCleanupQueueType, queue, token);
        /// <summary>
        /// Returns the amount of recurring jobs in <paramref name="queue"/> that still need to be processed.
        /// </summary>
        /// <param name="queue">The queue to get the count for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The estimated amount of jobs in <paramref name="queue"/></returns>
        Task<long> GetRecurringJobProcessQueueLengthAsync(string queue, CancellationToken token = default) => GetQueueLengthAsync(HiveMindConstants.Queue.RecurringJobProcessQueueType, queue, token);

        /// <summary>
        /// Dequeues the next <paramref name="amount"/> jobs from queues <paramref name="queues"/> of type <paramref name="queueType"/>.
        /// </summary>
        /// <param name="queueType">The type of the queue to dequeue from</param>
        /// <param name="queues">The names of the queues to dequeue from</param>
        /// <param name="amount">How many jobs to dequeue</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all the jobs that were dequeued or an empty array if <paramref name="queues"/> of type <paramref name="queueType"/> is empty</returns>
        Task<IDequeuedJob[]> DequeueAsync(string queueType, IEnumerable<string> queues, int amount, CancellationToken token = default);
    }
}
