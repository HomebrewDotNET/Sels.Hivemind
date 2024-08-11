using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Sels.Core.Dispose;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Threading;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.DistributedLocking;
namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// A <see cref="IDequeuedJob"/> that is also locked with an extra <see cref="IDistributedLock"/>.
    /// </summary>
    public class DistributedLockedDequeuedJob : IDequeuedJob, IDistributedLock, IAsyncExposedDisposable
    {
        // Fields
        private readonly List<Func<Task>> _onExpiredActions = new List<Func<Task>>();
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private readonly IDequeuedJob _job;
        private readonly IDistributedLock _lock;

        // State
        private bool _isExpired;

        /// <inheritdoc/>
        public string JobId => _job.JobId;
        /// <inheritdoc/>
        public Guid ExecutionId => _job.ExecutionId;
        /// <inheritdoc/>
        public string Queue => _job.Queue;
        /// <inheritdoc/>
        public string Type => _job.Type;
        /// <inheritdoc/>
        public QueuePriority Priority => _job.Priority;
        /// <inheritdoc/>
        public DateTime EnqueuedAtUtc => _job.EnqueuedAtUtc;
        /// <inheritdoc/>
        public DateTime ExpectedTimeoutUtc => _job.ExpectedTimeoutUtc <= _lock.ExpectedTimeoutUtc ? _job.ExpectedTimeoutUtc : _lock.ExpectedTimeoutUtc;
        /// <inheritdoc/>
        public bool IsSelfManaged => _job.IsSelfManaged && _lock.IsSelfManaged;
        /// <inheritdoc/>
        public string Resource => _lock.Resource;
        /// <inheritdoc/>
        public string Environment => _lock.Environment;
        /// <inheritdoc/>
        public string Holder => _lock.Holder;
        /// <inheritdoc/>
        public bool IsExpired => _isExpired || _job.IsExpired || _lock.IsExpired;
        /// <inheritdoc/>
        public bool CanNotifyExpiry => true;
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }

        /// <inheritdoc cref="DistributedLockedDequeuedJob"/>
        /// <param name="job">The job that is locked with <paramref name="distributedLock"/></param>
        /// <param name="distributedLock">The distributed lock placed on <paramref name="job"/></param>
        public DistributedLockedDequeuedJob(IDequeuedJob job, IDistributedLock distributedLock)
        {
            _job = Guard.IsNotNull(job);
            _lock = Guard.IsNotNull(distributedLock);

            if(_job.SupportsExpiryNotification) _job.OnLockExpired(TriggerExpired);
            if(_lock.SupportsExpiryNotification) _lock.OnLockExpired(TriggerExpired);
        }

        /// <inheritdoc/>
        public Task CompleteAsync(CancellationToken token = default)
        {
            return _job.CompleteAsync(token);
        }
        /// <inheritdoc/>
        public Task DelayToAsync(DateTime delayToUtc, CancellationToken token = default)
        {
            return _job.DelayToAsync(delayToUtc, token);
        }

        /// <inheritdoc/>
        public async Task<bool> TryKeepAliveAsync(CancellationToken token = default)
        {
            await using var asyncLock = await _asyncLock.LockAsync(token).ConfigureAwait(false);

            // Check if already expired
            if (IsExpired) return false;

            // Try extend distributed lock if not self managed
            if (!_lock.IsSelfManaged)
            {
                if (!await _lock.TryKeepAliveAsync(token).ConfigureAwait(false))
                {
                    await TriggerExpired().ConfigureAwait(false);
                    return false;
                }
            }

            // Try extend job lock if not self managed
            if (!_job.IsSelfManaged)
            {
                if (!await _job.TryKeepAliveAsync(token).ConfigureAwait(false))
                {
                    await TriggerExpired().ConfigureAwait(false);
                    return false;
                }
            }           

            return true;
        }
        /// <inheritdoc/>
        public void OnLockExpired(Func<Task> action)
        {
            action = Guard.IsNotNull(action);

            bool registered = false;
            if(_job.SupportsExpiryNotification)
            {
                _job.OnLockExpired(action);
                registered = true;
            }
            if (_lock.SupportsExpiryNotification)
            {
                _lock.OnLockExpired(action);
                registered = true;
            }

            if(!registered) _onExpiredActions.Add(action);
        }

        private async Task TriggerExpired()
        {
            await _onExpiredActions.ForceExecuteAsync(x => x()).ConfigureAwait(false);
            _isExpired = true;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            await using var asyncLock = await _asyncLock.LockAsync().ConfigureAwait(false);
            if (IsDisposed.HasValue) return;
            using var disposing = new ExecutedAction(x => IsDisposed = x);
            var exceptions = new List<Exception>();
            // Release job first
            try
            {
                await _job.DisposeAsync().ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                exceptions.Add(ex);
            }

            // Release lock
            try
            {
                await _lock.DisposeAsync().ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                exceptions.Add(ex);
            }

            _isExpired = true;
            if(exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }
    }
}
