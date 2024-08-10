using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Collections;
using Sels.HiveMind.DistributedLocking;
using Sels.HiveMind.Extensions;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Scheduler
{
    /// <summary>
    /// Middleware that places a distributed lock on dequeued jobs to limit the amount of concurrent executions.
    /// </summary>
    public class DistributedLockJobSchedulerMiddleware : IJobSchedulerMiddleware
    {
        // Fields
        private readonly Guid _id = Guid.NewGuid();
        private readonly ILogger? _logger;
        private readonly ITaskManager _taskManager;
        private readonly IDistributedLockServiceProvider _distributedLockServiceProvider;

        // Properties
        /// <inheritdoc/>
        public byte? Priority { get; set; }
        /// <inheritdoc/>
        public string Environment { get; set; }

        /// <inheritdoc cref="DistributedLockJobSchedulerMiddleware"/>
        /// <param name="environment">The environment the middleware is configured in</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="priority"><inheritdoc cref="Priority"/></param>
        /// <param name="distributedLockServiceProvider">The provider to use to get the distributed lock service configured for <paramref name="environment"/></param>
        /// <param name="taskManager">Task manager used to manage tasks</param>
        /// <param name="logger">Optional logger for tracing</param>
        public DistributedLockJobSchedulerMiddleware(IDistributedLockServiceProvider distributedLockServiceProvider, ITaskManager taskManager, ILogger<DistributedLockJobSchedulerMiddleware>? logger = null)
        {
            _distributedLockServiceProvider = Guard.IsNotNull(distributedLockServiceProvider);
            _taskManager = Guard.IsNotNull(taskManager);
            _logger = logger;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IDequeuedJob> RequestAsync(IJobScheduler requester, IJobSchedulerQueueGroup group, string queueType, IEnumerable<IJobSchedulerQueueState> queues, int amount, object? context, Func<IJobScheduler, IEnumerable<IJobSchedulerQueueState>, int, CancellationToken, IAsyncEnumerable<IDequeuedJob>> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            requester = Guard.IsNotNull(requester);
            group = Guard.IsNotNull(group);
            queueType = Guard.IsNotNullOrWhitespace(queueType);
            queues = Guard.IsNotNull(queues);
            next = Guard.IsNotNull(next);

            DistributedLockJobSchedulerMiddlewareOptions options;
            if(context is DistributedLockJobSchedulerMiddlewareOptions contextOptions)
            {
                DistributedLockJobSchedulerMiddlewareOptionsValidationProfile.Instance.Validate(contextOptions, options: ObjectValidationFramework.Profile.ProfileExecutionOptions.ThrowOnError);
                options = contextOptions;
            }
            else
            {
                options = new DistributedLockJobSchedulerMiddlewareOptions();
            }

            _logger.Debug($"Resolving distributed locking service for environment {HiveLog.Environment} for scheduler <{requester}>", requester.Environment);
            await using var distributedLockServiceScope = await _distributedLockServiceProvider.CreateAsync(requester.Environment, cancellationToken).ConfigureAwait(false);
            var distributedLockService = distributedLockServiceScope.Component;
            var lockRequester = $"{requester.Name}.{nameof(DistributedLockJobSchedulerMiddleware)}-{_id}";

            bool restart = true;
            while (!cancellationToken.IsCancellationRequested && restart)
            {
                restart = false;
                await foreach (var job in next(requester, queues, amount, cancellationToken))
                {                   
                    var locks = GetPossibleLocks(job, options);
                    _logger.Log($"Trying to get a distributed lock on one of the <{locks.Length}> possible locks for <{HiveLog.Job.IdParam}> from queue <{HiveLog.Job.QueueParam}> of type <{queueType}>", job.JobId, job.Queue);

                    bool tryLock = true;
                    while (tryLock)
                    {
                        tryLock = false;

                        // Try lock
                        foreach (var lockName in OrderLocks(locks, job, options))
                        {
                            if (cancellationToken.IsCancellationRequested) yield break;
                            if (job.IsExpired) break;
                            using var logScope = _logger.TryBeginScope(x => x.AddFluently(HiveLog.DistributedLocking.Resource, lockName)
                                                                             .AddFluently(HiveLog.DistributedLocking.Process, lockRequester)
                                                                             .AddFluently(HiveLog.Job.Id, job.JobId)
                                                                             .AddFluently(HiveLog.Job.Queue, job.Queue)
                                                                             .AddFluently(HiveLog.Environment, requester.Environment));

                            // Try acquire
                            _logger.Debug($"Trying to acquire lock");

                            if (options.Timeout.HasValue && options.Timeout.Value == TimeSpan.Zero)
                            {
                                if (await distributedLockService.TryAcquireAsync(lockName, lockRequester, cancellationToken).ConfigureAwait(false) is (true, var distributedLock))
                                {
                                    _logger.Log($"Acquired lock");
                                    yield return new DistributedLockedDequeuedJob(job, distributedLock!);
                                    break;
                                }
                            }
                            else
                            {
                                IDistributedLock? distributedLock = null;
                                try
                                {
                                    distributedLock = await distributedLockService.AcquireAsync(lockName, lockRequester, options.Timeout, cancellationToken).ConfigureAwait(false);
                                    _logger.Log($"Acquired lock");
                                    distributedLock = new DistributedLockedDequeuedJob(job, distributedLock!);
                                }
                                catch (TimeoutException)
                                {
                                    _logger.Debug($"Could not acquire lock within timeout of <{options.Timeout}>");
                                }
                                if (distributedLock != null)
                                {
                                    yield return new DistributedLockedDequeuedJob(job, distributedLock!);
                                    break;
                                }
                            }

                            // Not acquired
                            _logger.Log($"Could not acquire lock");
                            switch (options.NotAcquiredBehaviour)
                            {
                                case DistributedLockNotAcquiredBehaviour.ReturnAndSleep:
                                    _logger.Log($"Could not acquire lock. Job will be returned to queue and middleware will sleep for <{options.DelayTime}>");
                                    await job.DisposeAsync().ConfigureAwait(false);
                                    await Helper.Async.Sleep(options.DelayTime, cancellationToken).ConfigureAwait(false);
                                    restart = true;
                                    break;
                                case DistributedLockNotAcquiredBehaviour.KeepAndSleep:
                                    await using (job.KeepAliveDuringScope(this, $"KeepAlive-{job.JobId}", _taskManager, options.DelayTime, () =>
                                    {
                                        _logger.Warning($"Lock on <{lockName}> expired while trying to keep it alive");
                                        restart = true;
                                        return Task.CompletedTask;
                                    }, _logger, cancellationToken))
                                    {
                                        await Helper.Async.Sleep(options.DelayTime, cancellationToken).ConfigureAwait(false);
                                        tryLock = true;
                                    }
                                    break;
                                case DistributedLockNotAcquiredBehaviour.ReturnAndDisableQueue:
                                    _logger.Log($"Could not acquire lock. Disabling queue <{HiveLog.Job.Queue}> for <{options.DelayTime}>", job.Queue);
                                    var queue = requester.Queues.CurrentQueueGroups.SelectMany(x => x.CurrentQueues).FirstOrDefault(x => job.Queue.Equals(x.Name));
                                    await job.DisposeAsync().ConfigureAwait(false);
                                    if (queue != null) queue.ActiveAfter = DateTime.Now.Add(options.DelayTime);
                                    restart = true;
                                    break;
                                case DistributedLockNotAcquiredBehaviour.ReturnAndRemoveQueue:
                                    _logger.Log($"Could not acquire lock. Removing queue <{HiveLog.Job.Queue}>", job.Queue);
                                    requester.Queues.RemoveQueue(job.Queue);
                                    await job.DisposeAsync().ConfigureAwait(false);
                                    restart = true;
                                    break;
                                default:
                                    throw new NotSupportedException($"Behaviour {options.NotAcquiredBehaviour} is not supported");
                            }

                            if (restart) break;
                        }
                    }
                    
                    if (restart) break;
                }
            }           
        }

        private string[] GetPossibleLocks(IDequeuedJob job, DistributedLockJobSchedulerMiddlewareOptions options)
        {
            job = Guard.IsNotNull(job);
            options = Guard.IsNotNull(options);

            return Enumerable.Range(1, options.MaxConcurrency).Select(x =>
            {
                var builder = new StringBuilder();
                if (options.LockNamePrefix.HasValue()) builder.Append(options.LockNamePrefix);
                if (options.LockName.HasValue())
                {
                    builder.Append(options.LockName);
                }
                else
                {
                    builder.Append(job.Queue);
                }

                if(options.MaxConcurrency > 1) builder.Append($"-{x}");
                return builder.ToString();
            }).ToArray();
        }

        private IEnumerable<string> OrderLocks(string[] locks, IDequeuedJob job, DistributedLockJobSchedulerMiddlewareOptions options)
        {
            locks = Guard.IsNotNull(locks);
            job = Guard.IsNotNull(job);
            options = Guard.IsNotNull(options);

            if (options.AlwaysLockInOrder || options.MaxConcurrency == 1) { 
                foreach(var lockName in locks)
                {
                    yield return lockName;
                }
            }
        
            // Parition using hashcode of id so we get the index of the first lock to try
            var index = Helper.Paritioning.Partition(job.JobId.GetHashCode(), options.MaxConcurrency);
            foreach(var lockName in locks.Skip(index).Concat(locks.Take(index)))
            {
                yield return lockName;
            }
        }
    }
}
