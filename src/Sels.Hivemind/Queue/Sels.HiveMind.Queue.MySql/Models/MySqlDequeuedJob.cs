using Sels.Core;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.HiveMind.HiveMindConstants;

namespace Sels.HiveMind.Queue.MySql
{
    /// <summary>
    /// Represents a job that was retrieved from a MySql queue.
    /// Disposing will return the job to the queue if no other action was triggered.
    /// </summary>
    public class MySqlDequeuedJob : IDequeuedJob
    {
        // Fields
        private readonly HiveMindMySqlQueue _source;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        // State
        private bool _handled;

        // Properties
        /// <summary>
        /// The id of the dequeuedjob entry.
        /// </summary>
        public long Id { get; }
        /// <inheritdoc/>
        public string JobId { get; }
        /// <inheritdoc/>
        public Guid ExecutionId { get; }
        /// <inheritdoc/>
        public string Queue { get; }
        /// <inheritdoc/>
        public string Type { get; }
        /// <inheritdoc/>
        public QueuePriority Priority { get; }
        /// <inheritdoc/>
        public DateTime EnqueuedAtUtc { get; }
        /// <inheritdoc/>
        public DateTime ExpectedTimeoutUtc { get; }
        /// <inheritdoc/>
        public bool IsValid => DateTime.UtcNow < ExpectedTimeoutUtc;

        /// <inheritdoc cref="MySqlDequeuedJob"/>
        /// <param name="queue">The instance that created this instance</param>
        /// <param name="table">The instance to copy from</param>
        /// <param name="queueType"><inheritdoc cref="Type"/></param>
        public MySqlDequeuedJob(HiveMindMySqlQueue queue, MySqlJobQueueTable table, string queueType)
        {
            _source = queue.ValidateArgument(nameof(queue));
            table.ValidateArgument(nameof(table));
            Id = table.Id;
            JobId = table.JobId;
            ExecutionId = Guid.Parse(table.ExecutionId);
            Queue = table.Name;
            Type = queueType;
            Priority = table.Priority;
            EnqueuedAtUtc = table.EnqueuedAt;
            ExpectedTimeoutUtc = table.FetchedAt.Value + queue.Options.LockTimeout;
        }

        /// <inheritdoc/>
        public Task<bool> TryKeepAliveAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task CompleteAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task DelayToAsync(DateTime delayToUtc, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
