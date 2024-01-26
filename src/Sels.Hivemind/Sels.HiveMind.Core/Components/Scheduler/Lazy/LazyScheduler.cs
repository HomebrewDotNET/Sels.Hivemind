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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler.Lazy
{
    /// <summary>
    /// Shceduler where each requesting consumer does it's own dequeue. Once the queues are empty a single thread will be spawned to monitor the queue.
    /// </summary>
    public class LazyScheduler : IJobScheduler, IAsyncDisposable
    {
        // Fields
        private readonly IOptionsMonitor<LazySchedulerOptions> _options;
        private readonly ILogger _logger;
        private readonly WorkerQueue<IDequeuedJob> _workerQueue;
        private readonly QueueGroup[] _queueGroups;

        // State
        private TaskCompletionSource<bool> _waitHandle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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

        /// <inheritdoc cref="LazyScheduler"/>
        /// <param name="name">The name assigned to the scheduler</param>
        /// <param name="queueType">The type of queue to poll</param>
        /// <param name="queueGroups">The groups of queues to retrie jobs from</param>
        /// <param name="levelOfConcurrency">With how many concurrent threads the caller will poll</param>
        /// <param name="queue">The job queue to poll</param>
        /// <param name="taskManager">Task manager used to manage recurring tasks</param>
        /// <param name="options">Used to fetch the options for this scheduler</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <exception cref="NotSupportedException"></exception>
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
            _ = _workerQueue.OnRequestCreated(t =>
            {
                ReleaseMonitor();
                return Task.CompletedTask;
            });
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

            IDequeuedJob job = null;

            try
            {
                await foreach (var dequeuedJob in FetchJobsAsync(FetchSize, token))
                {
                    if (job == null)
                    {
                        job = dequeuedJob;
                    }
                    else
                    {
                        if (!await _workerQueue.TryEnqueueAsync(dequeuedJob, token).ConfigureAwait(false))
                        {
                            await dequeuedJob.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.Warning($"Request cancelled while checking queue");
            }
            catch (Exception ex)
            {
                _logger.Log($"Something went wrong while fetching the next <{FetchSize}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>", ex);

                if (job != null) await job.DisposeAsync().ConfigureAwait(false);
            }

            if(job == null) _logger.Debug($"Queues <{QueueGroupDisplay}> of type <{QueueType}> are all empty");

            return job;
        }

        private async Task MonitorQueue(CancellationToken token)
        {
            _logger.Log($"{Name} starting queue monitor");

            while (!token.IsCancellationRequested)
            {
                Task sleepTask = null;
                lock (_waitHandle)
                {
                    sleepTask = _waitHandle.Task;
                }
                if(_workerQueue.PendingRequests <= 0) await Helper.Async.WaitOn(sleepTask, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                while (_workerQueue.PendingRequests > 0)
                {
                    try
                    {
                        _logger.Debug($"{Name} got <{_workerQueue.PendingRequests}> pending requests. Checking queue");
                        var fetchSize = FetchSize * _workerQueue.PendingRequests;
                        if (fetchSize > GlobalLimit) fetchSize = GlobalLimit;
                        await foreach(var job in FetchJobsAsync(fetchSize, token).ConfigureAwait(false) )
                        {
                            _logger.Debug($"{Name} got job <{job.JobId}> to fulfill pending requests");
                            try
                            {
                                if (!await _workerQueue.TryEnqueueAsync(job, token).ConfigureAwait(false))
                                {
                                    await job.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Log($"Something went wrong for {Name} while fetching the next <{FetchSize}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>", ex);
                            }
                        }
                    }
                    catch(OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        _logger.Warning($"{Name} cancelled monitoring for new jobs");
                    }
                    catch(Exception ex)
                    {
                        _logger.Log($"{Name} ran into issue while monitoring for new jobs", ex);
                    }

                    // Sleep
                    if (_workerQueue.PendingRequests > 0)
                    {
                        var sleepTime = _options.Get(Name).PollingInterval;
                        _logger.Debug($"{Name} will check queue in <{sleepTime}> to fulfill pending requests");
                        await Helper.Async.Sleep(sleepTime, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;
                    }
                }
            }

            _logger.Log($"{Name} queue monitor stopping");
        }

        /// <summary>
        /// Fetches the next <paramref name="amount"/> job(s) from queues <see cref="QueueGroups"/> of type <see cref="QueueType"/>.
        /// </summary>
        /// <param name="token">Token to cancel the fetch</param>
        /// <returns>The next <paramref name="amount"/> job(s) if there are any, otherwise empty array if the queues are empty</returns>
        protected virtual async IAsyncEnumerable<IDequeuedJob> FetchJobsAsync(int amount, [EnumeratorCancellation] CancellationToken token)
        {
            _logger.Debug($"Fetching the next <{amount}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>");
            int returned = 0;

            foreach (var (queueGroup, i) in GetActiveGroups().Select((x, i) => (x, i)))
            {
                if (returned >= amount) yield break;
                var queues = queueGroup.Queues;
                _logger.Debug($"Fetching the next <{amount}> job(s) from queues <{queues.JoinString('|')}> of type <{QueueType}>");

                var dequeued = await Queue.DequeueAsync(QueueType, queues, FetchSize, token);

                if (dequeued.HasValue())
                {
                    _logger.Debug($"Fetched <{dequeued.Length}> job(s) from queues <{queues.JoinString('|')}> of type <{QueueType}>");

                    foreach(var job in dequeued)
                    {
                        returned++;
                        yield return job;
                    }
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
        }

        /// <inheritdoc/>
        public async Task<IDequeuedJob> RequestAsync(CancellationToken token)
        {
            _logger.Log($"{Name} requesting the next job from queues <{QueueGroupDisplay}> of type <{QueueType}>");

            var job = await _workerQueue.DequeueAsync(token);
            _logger.Log($"{Name} received job <{job.JobId}> of priority <{job.Priority}> from queue <{job.Queue}> of type <{job.Type}>");
            return job;
        }

        private void ReleaseMonitor()
        {
            lock (_waitHandle)
            {
                _waitHandle.TrySetResult(true);
                _waitHandle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _workerQueue.DisposeAsync().ConfigureAwait(false);
        }

        private IEnumerable<QueueGroup> GetActiveGroups()
        {
            var checkDelay = _options.Get(Name).EmptyQueueCheckDelay;
            foreach (var group in _queueGroups)
            {
                if (group.LastEmptyDate.Add(checkDelay) > DateTime.Now) continue;

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
