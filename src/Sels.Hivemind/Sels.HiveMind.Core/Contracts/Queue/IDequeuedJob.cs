using Sels.Core.Extensions;
using Sels.Core.Scope.AsyncActions;
using Sels.HiveMind.Queue;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Represents a job that was retrieved from a queue.
    /// Disposing will return the job to the queue if no other action was triggered.
    /// </summary>
    public interface IDequeuedJob : IAsyncDisposable
    {
        /// <summary>
        /// The id of the job that was dequeued.
        /// </summary>
        public string JobId { get; }
        /// <summary>
        /// The execution id of the job when it was enqueued.
        /// </summary>
        public Guid ExecutionId { get; }
        /// <summary>
        /// The name of the queue the job was placed in.
        /// </summary>
        public string Queue { get; }
        /// <summary>
        /// The type of the queue the job was placed in.
        /// </summary>
        public string Type { get; }
        /// <summary>
        /// The priority of the enqueued job.
        /// </summary>
        public QueuePriority Priority { get; }
        /// <summary>
        /// The date (in utc) when the job was enqueued.
        /// </summary>
        public DateTime EnqueuedAtUtc { get; }
        /// <summary>
        /// The date (local machine) when the job was enqueued.
        /// </summary>
        public DateTime EnqueuedAt => EnqueuedAtUtc.ToLocalTime();

        /// <summary>
        /// True if the dequeued job is still valid (lock still valid, ...), otherwise false.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Tries to keep the current dequeued job alive. Needs to be called to ensure dequeued job stays locked during processing.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the dequeued job is still valid, otherwise false</returns>
        public Task<bool> TryKeepAliveAsync(CancellationToken token = default);
        /// <summary>
        /// Completes the current dequeued job so it can be removed from the queue.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        public Task CompleteAsync(CancellationToken token = default);

        /// <summary>
        /// Delays the current dequeued job so it can be processed again in the future.
        /// </summary>
        /// <param name="delayToUtc">The date (in utc) to delay to</param>
        /// <param name="token">Optional token to cancel the request</param>
        public Task DelayToAsync(DateTime delayToUtc, CancellationToken token = default);
    }
}
