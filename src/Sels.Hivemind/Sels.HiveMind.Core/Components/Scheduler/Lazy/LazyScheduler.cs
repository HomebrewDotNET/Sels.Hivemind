using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Async.Queue;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler.Lazy
{
    public class LazyScheduler : IJobScheduler, IAsyncDisposable
    {
        // Fields
        private readonly IOptionsMonitor<LazySchedulerOptions> _options;
        private readonly ILogger _logger;
        private readonly WorkerQueue<IDequeuedJob> _workerQueue;
        private readonly QueueGroup[] _queueGroups;

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public string QueueType { get; }
        /// <inheritdoc/>
        public IReadOnlyList<IReadOnlyCollection<string>> QueueGroups => _queueGroups.Select(x => x.Queues).ToArray();
        /// <inheritdoc/>
        public IJobQueue Queue { get; }
        /// <inheritdoc/>
        public int LevelOfConcurrency { get; }
        /// <summary>
        /// The maximum amount of job that can be cached locally. 
        /// </summary>
        public int GlobalLimit => LevelOfConcurrency * _options.Get(Name).PrefetchMultiplier;
        /// <summary>
        /// How many jobs are fetched when jobs are requested.
        /// </summary>
        public int FetchSize => _options.Get(Name).PrefetchMultiplier;
        /// <summary>
        /// Display string of the queues the current scheduler can fetch from ordered by priority.
        /// </summary>
        public string QueueGroupDisplay => QueueGroups.Select(x => $"({x.JoinString('|')})").JoinString("=>");

        public LazyScheduler(string name, string queueType, IEnumerable<IEnumerable<string>> queueGroups, int levelOfConcurrency, IJobQueue queue, ITaskManager taskManager, IOptionsMonitor<LazySchedulerOptions> options, ILogger<LazyScheduler> logger = null)
        {
            Name = name.ValidateArgument(nameof(name));
            QueueType = queueType.ValidateArgument(nameof(queueType));
            _queueGroups = queueGroups.ValidateArgumentNotNullOrEmpty(nameof(queueGroups)).Select((x, i) => x.ValidateArgumentNotNullOrEmpty($"{nameof(queueGroups)}[{i}]").ToList()).Select(x => new QueueGroup(x)).ToArray();
            LevelOfConcurrency = levelOfConcurrency.ValidateArgumentLargerOrEqual(nameof(levelOfConcurrency), 1);
            Queue = queue.ValidateArgument(nameof(queue));
            if (!queue.Features.HasFlag(JobQueueFeatures.Polling)) throw new NotSupportedException($"{nameof(LazyScheduler)} only supports queues that support polling");
            _options = options.ValidateArgument(nameof(options));
            _logger = logger;
            taskManager.ValidateArgument(nameof(taskManager));

            _workerQueue = new WorkerQueue<IDequeuedJob>(taskManager, GlobalLimit, logger);
            _ = _workerQueue.InterceptRequest(RequestJobsAsync);
            // Start task and tie ownership to queue
            taskManager.TryScheduleAction(_workerQueue, "QueueMonitor", false, MonitorQueue, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepAlive));
        }

        /// <summary>
        /// Fetches the next <see cref="FetchSize"/> job(s) from queues <see cref="QueueGroups"/> of type <see cref="QueueType"/>.
        /// </summary>
        /// <param name="token">Token to cancel the fetch</param>
        /// <returns>The next available job if there are any, otherwise null if the queues are empty</returns>
        protected virtual async Task<IDequeuedJob> RequestJobsAsync(CancellationToken token)
        {
            _logger.Debug($"Requesting the next <{FetchSize}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>");

            var dequeued = await FetchJobsAsync(FetchSize, token).ConfigureAwait(false);

            if (dequeued.HasValue())
            {
                try
                {
                    // Cache extra jobs
                    foreach (var other in dequeued.Skip(1))
                    {
                        if (!await _workerQueue.TryEnqueueAsync(other, token).ConfigureAwait(false))
                        {
                            await other.DisposeAsync().ConfigureAwait(false);
                        }
                    }

                    return dequeued[0];
                }
                catch (Exception ex)
                {
                    _logger.Log($"Something went wrong while fetching the next <{FetchSize}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>", ex);

                    await Task.WhenAll(dequeued.Select(x => x.DisposeAsync().AsTask())).ConfigureAwait(false);
                }
            }

            _logger.Debug($"Queues <{QueueGroupDisplay}> of type <{QueueType}> are all empty");

            return null;
        }

        private async Task MonitorQueue(CancellationToken token)
        {
            _logger.Log($"{Name} starting queue monitor");

            while (!token.IsCancellationRequested)
            {
                var sleepTime = _options.Get(Name).PollingInterval;
                _logger.Debug($"{Name} will check queue in <{sleepTime}> if there are any pending requests");
                await Helper.Async.Sleep(sleepTime, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                if (_workerQueue.PendingRequests > 0)
                {
                    _logger.Debug($"{Name} got <{_workerQueue.PendingRequests}> pending requests. Checking queue");

                    var dequeued = await FetchJobsAsync(FetchSize, token).ConfigureAwait(false);

                    if (dequeued.HasValue())
                    {
                        _logger.Debug($"{Name} got <{dequeued.Length}> jobs to fulfill pending requests");
                        try
                        {
                            foreach (var other in dequeued)
                            {
                                if (!await _workerQueue.TryEnqueueAsync(other, token).ConfigureAwait(false))
                                {
                                    await other.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Something went wrong while fetching the next <{FetchSize}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>", ex);

                            await Task.WhenAll(dequeued.Select(x => x.DisposeAsync().AsTask())).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    _logger.Debug($"No pending requests for <{Name}>. Sleeping");
                }
            }

            _logger.Log($"{Name} queue monitor stopping");
        }

        /// <summary>
        /// Fetches the next <paramref name="amount"/> job(s) from queues <see cref="QueueGroups"/> of type <see cref="QueueType"/>.
        /// </summary>
        /// <param name="token">Token to cancel the fetch</param>
        /// <returns>The next <paramref name="amount"/> job(s) if there are any, otherwise empty array if the queues are empty</returns>
        protected virtual async Task<IDequeuedJob[]> FetchJobsAsync(int amount, CancellationToken token)
        {
            _logger.Debug($"Fetching the next <{amount}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>");

            foreach (var (queueGroup, i) in GetActiveGroups().Select((x, i) => (x, i)))
            {
                var queues = queueGroup.Queues;
                _logger.Debug($"Fetching the next <{amount}> job(s) from queues <{queues.JoinString('|')}> of type <{QueueType}>");

                var dequeued = await Queue.DequeueAsync(QueueType, queues, FetchSize, token);

                if (dequeued.HasValue())
                {
                    _logger.Debug($"Fetched <{dequeued.Length}> job(s) from queues <{queues.JoinString('|')}> of type <{QueueType}>");

                    return dequeued;
                }
                else
                {
                    _logger.Debug($"Nothing in queues <{queues.JoinString('|')}> of type <{QueueType}>. Checking next group");
                    lock (queueGroup)
                    {
                        queueGroup.LastEmptyDate = DateTime.Now;
                    }
                }
            }

            _logger.Debug($"Queues <{QueueGroupDisplay}> of type <{QueueType}> are all empty");

            return Array.Empty<IDequeuedJob>();
        }

        /// <inheritdoc/>
        public async Task<IDequeuedJob> RequestAsync(CancellationToken token)
        {
            _logger.Log($"{Name} requesting the next job from queues <{QueueGroupDisplay}> of type <{QueueType}>");

            var job = await _workerQueue.DequeueAsync(token);
            _logger.Log($"{Name} received job <{job.JobId}> of priority <{job.Priority}> from queue <{job.Queue}> of type <{job.Type}>");
            return job;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync() => await _workerQueue.DisposeAsync().ConfigureAwait(false);

        private IEnumerable<QueueGroup> GetActiveGroups()
        {
            foreach(var group in _queueGroups)
            {
                lock (group)
                {
                    if (group.LastEmptyDate.Add(_options.CurrentValue.EmptyQueueCheckDelay) > DateTime.Now) continue;
                }

                yield return group;
            }
        }

        private class QueueGroup
        {
            // Properties
            public IReadOnlyCollection<string> Queues { get; }
            public DateTime LastEmptyDate { get; set; }

            public QueueGroup(IEnumerable<string> queues)
            {
                Queues = queues.ValidateArgumentNotNullOrEmpty(nameof(queues)).ToArray();
            }
        }
    }
}
