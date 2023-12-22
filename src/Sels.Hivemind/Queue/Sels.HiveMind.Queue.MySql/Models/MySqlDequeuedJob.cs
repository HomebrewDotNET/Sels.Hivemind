using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Threading;
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
        private readonly string _processId;

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
        public bool IsSelfManaged => false;

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
            _processId = table.ProcessId.ValidateArgumentNotNullOrWhitespace(nameof(table.ProcessId));
        }

        /// <inheritdoc/>
        public async Task<bool> TryKeepAliveAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                if(!await _source.TrySetHeartbeatAsync(Id, Type, _processId, token).ConfigureAwait(false))
                {
                    _handled = true;
                    return false;
                }
                return true;
            }
        }

        /// <inheritdoc/>
        public async Task CompleteAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                if (!await _source.TryDeleteAsync(Id, Type, _processId, token).ConfigureAwait(false))
                {
                    throw new DequeuedJobLockStaleException(JobId, Queue, Type, _source.Environment);
                }
                _handled = true;
            }
        }

        /// <inheritdoc/>
        public async Task DelayToAsync(DateTime delayToUtc, CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                if (!await _source.TryDelayToAsync(Id, Type, _processId, delayToUtc, token).ConfigureAwait(false))
                {
                    throw new DequeuedJobLockStaleException(JobId, Queue, Type, _source.Environment);
                }
                _handled = true;
            }
        }
        /// <inheritdoc/>
        public void OnLockExpired(Func<Task> action) => throw new NotSupportedException();

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await using (await _lock.LockAsync().ConfigureAwait(false))
            {
                if (!_handled)
                {
                    if (!await _source.TryReleaseAsync(Id, Type, _processId, default).ConfigureAwait(false))
                    {
                        throw new DequeuedJobLockStaleException(JobId, Queue, Type, _source.Environment);
                    }
                }
            }
        }
    }
}
