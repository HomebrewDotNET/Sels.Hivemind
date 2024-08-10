using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Async.Queue;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
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

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Shceduler where each requesting consumer does it's own dequeue. Once the queues are empty a single thread will be spawned to monitor the queue.
    /// </summary>
    public class PullthroughScheduler : IJobScheduler, IAsyncDisposable
    {
        // Statics 
        /// <summary>
        /// The type of the scheduler.
        /// </summary>
        public static string SchedulerType => "Pullthrough";

        // Fields
        private readonly IOptionsMonitor<PullthroughSchedulerOptions> _optionsMonitor;
        private readonly PullthroughSchedulerOptions _options;
        private readonly ILogger? _logger;
        private WorkerQueue<IDequeuedJob> _workerQueue;

        // State
        private TaskCompletionSource<bool> _waitHandle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private PullthroughSchedulerOptions Options => _optionsMonitor?.Get(Name) ?? _options;

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public string QueueType { get; }
        /// <inheritdoc/>
        public IJobSchedulerQueues Queues { get; set; }
        /// <inheritdoc/>
        public IJobSchedulerRequestPipeline RequestPipeline { get; set; }
        /// <inheritdoc/>
        public int LevelOfConcurrency { get; }
        /// <summary>
        /// The maximum amount of job that can be cached locally. 
        /// </summary>
        public int GlobalLimit => LevelOfConcurrency * Options.PrefetchMultiplier;
        /// <summary>
        /// How many jobs are fetched when jobs are requested.
        /// </summary>
        public int FetchSize => Options.PrefetchMultiplier;
        /// <summary>
        /// Display string of the queues the current scheduler can fetch from ordered by priority.
        /// </summary>
        public string QueueGroupDisplay => Queues.CurrentQueueGroups.Select(x => $"({x.CurrentQueues.Select(x => x.Name).JoinString('|')})").JoinString("=>");
        /// <inheritdoc/>
        public string Environment { get; set; }

        /// <inheritdoc cref="PullthroughScheduler"/>
        /// <param name="name">The name assigned to the scheduler</param>
        /// <param name="queueType">The type of queue to poll</param>
        /// <param name="levelOfConcurrency">With how many concurrent threads the caller will poll</param>
        /// <param name="taskManager">Task manager used to manage recurring tasks</param>
        /// <param name="options">Used to fetch the options for this scheduler</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <exception cref="NotSupportedException"></exception>
        public PullthroughScheduler(IOptionsMonitor<PullthroughSchedulerOptions> options, string name, string queueType, int levelOfConcurrency, ITaskManager taskManager, ILogger<PullthroughScheduler>? logger = null)
            : this(name, queueType, levelOfConcurrency, taskManager, logger)
        {
            _optionsMonitor = options.ValidateArgument(nameof(options));
            CreateWorkerQueue(taskManager);
        }

        public PullthroughScheduler(PullthroughSchedulerOptions options, JobSchedulerConfiguration configuration, ITaskManager taskManager, ILogger<PullthroughScheduler>? logger = null)
            : this(configuration.ValidateArgument(nameof(configuration)).Name, configuration.QueueType, configuration.LevelOfConcurrency, taskManager, logger)
        {
            _options = options.ValidateArgument(nameof(options));
            PullthroughSchedulerOptionsValidationProfile.Instance.Validate(_options, options: ObjectValidationFramework.Profile.ProfileExecutionOptions.ThrowOnError);

            CreateWorkerQueue(taskManager);
        }

        private PullthroughScheduler(string name, string queueType, int levelOfConcurrency, ITaskManager taskManager, ILogger<PullthroughScheduler>? logger = null)
        {
            Name = name.ValidateArgument(nameof(name));
            QueueType = queueType.ValidateArgument(nameof(queueType));
            LevelOfConcurrency = levelOfConcurrency.ValidateArgumentLargerOrEqual(nameof(levelOfConcurrency), 1);

            _logger = logger;          
        }

        private void CreateWorkerQueue(ITaskManager taskManager)
        {
            taskManager.ValidateArgument(nameof(taskManager));
            _workerQueue = new WorkerQueue<IDequeuedJob>(taskManager, GlobalLimit, _logger);
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
        protected virtual async Task<IDequeuedJob?> RequestJobsAsync(CancellationToken token)
        {
            _logger.Debug($"Requesting the next <{FetchSize}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>");

            IDequeuedJob? job = null;

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
                _logger.Debug($"Request cancelled while checking queue");
                return null;
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
                Task sleepTask;
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
                        var sleepTime = Options.PollingInterval;
                        _logger.Debug($"{Name} will check queue in <{sleepTime}> to fulfill pending requests");
                        await Helper.Async.Sleep(sleepTime, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;
                    }
                }
            }

            _logger.Log($"{Name} queue monitor stopping");
        }

        /// <summary>
        /// Fetches the next <paramref name="amount"/> job(s) from queues <see cref="Queues"/> of type <see cref="QueueType"/>.
        /// </summary>
        /// <param name="token">Token to cancel the fetch</param>
        /// <returns>The next <paramref name="amount"/> job(s) if there are any, otherwise empty array if the queues are empty</returns>
        protected virtual async IAsyncEnumerable<IDequeuedJob> FetchJobsAsync(int amount, [EnumeratorCancellation] CancellationToken token)
        {
            _logger.Debug($"Fetching the next <{amount}> job(s) from queues <{QueueGroupDisplay}> of type <{QueueType}>");
            int returned = 0;

            foreach (var queueGroup in Queues.GetActiveQueueGroups())
            {
                if (returned >= amount) yield break;
                var queues = queueGroup.CurrentActiveQueues.ToDictionary(x => x.Name, x => x);
                _logger.Debug($"Fetching the next <{amount}> job(s) from queues <{queues.JoinString('|')}> of type <{QueueType}>");

                var jobs = RequestPipeline.RequestAsync(this, QueueType, queueGroup, amount-returned, token);

                if (jobs != null)
                {
                    _logger.Debug($"Enumerating jobs from queues <{queues.JoinString('|')}> of type <{QueueType}>");

                    await foreach(var job in jobs)
                    {
                        _logger.Debug($"Retrieved job <{job.JobId}> from queue <{job.Queue}> of type <{QueueType}>");
                        queues.GetValueOrDefault(job.Queue)?.SetRetrieved(1);
                        returned++;
                        yield return job;
                    }
                }
                else
                {
                    _logger.Debug($"Nothing in queues <{queues.JoinString('|')}> of type <{QueueType}>. Checking next group");
                    var checkDelay = Options.EmptyQueueCheckDelay;
                    var activationDate = DateTime.Now.Add(checkDelay);
                    queues.Values.Execute(x =>
                    {
                        x.LastEmpty = DateTime.Now;
                        x.ActiveAfter = activationDate;
                    });
                }
            }

            _logger.Debug($"Queues <{QueueGroupDisplay}> of type <{QueueType}> are all empty");
        }

        /// <inheritdoc/>
        public async Task<IDequeuedJob> RequestAsync(CancellationToken token)
        {
            Guard.IsNotNull(Queues);
            Guard.IsNotNull(RequestPipeline);
            if (!RequestPipeline.Queue.Features.HasFlag(JobQueueFeatures.Polling)) throw new NotSupportedException($"{nameof(PullthroughScheduler)} only supports queues that support polling");
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
    }
}
