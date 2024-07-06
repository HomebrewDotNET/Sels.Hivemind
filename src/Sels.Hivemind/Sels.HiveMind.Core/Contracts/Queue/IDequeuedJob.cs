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
    public interface IDequeuedJob : ILockable, IAsyncDisposable
    {
        /// <summary>
        /// The id of the job that was dequeued.
        /// </summary>
        [Traceable(HiveLog.Job.Id)]
        public string JobId { get; }
        /// <summary>
        /// The execution id of the job when it was enqueued.
        /// </summary>
        [Traceable(HiveLog.Job.ExecutionId)]
        public Guid ExecutionId { get; }
        /// <summary>
        /// The name of the queue the job was placed in.
        /// </summary>
        [Traceable(HiveLog.Job.Queue)]
        public string Queue { get; }
        /// <summary>
        /// The type of the queue the job was placed in.
        /// </summary>
        public string Type { get; }
        /// <summary>
        /// The priority of the enqueued job.
        /// </summary>
        [Traceable(HiveLog.Job.Priority)]
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
