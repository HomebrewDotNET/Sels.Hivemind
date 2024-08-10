using Sels.Core.Dispose;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Threading;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Scheduler
{
    /// <inheritdoc cref="IJobSchedulerRequestPipeline"/>
    public class SchedulerRequestPipeline : IJobSchedulerRequestPipeline, IAsyncExposedDisposable
    {
        // Fields
        private readonly ILogger? _logger;
        private readonly IComponent<IJobSchedulerMiddleware>[]? _ownedMiddleware;

        //Properties
        /// <inheritdoc/>
        public SemaphoreSlim AsyncLock { get; } = new SemaphoreSlim(1, 1);
        /// <inheritdoc/>
        public IJobQueue Queue { get; }
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }
        private IJobSchedulerRequestPipeline Self => this;

        /// <inheritdoc cref="SchedulerRequestPipeline"/>
        /// <param name="queue"><inheritdoc cref="Queue"/></param>
        /// <param name="ownedMiddleware">Middleware that is owned by the current instance. These instances will also be disposed when the current pipeline is disposed</param>
        public SchedulerRequestPipeline(IJobQueue queue, IEnumerable<IComponent<IJobSchedulerMiddleware>>? ownedMiddleware, ILogger? logger = null)
        {
            Queue = Guard.IsNotNull(queue);
            _ownedMiddleware = ownedMiddleware.HasValue() ? ownedMiddleware!.ToArray() : null;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IDequeuedJob> RequestAsync(IJobScheduler requester, string queueType, IJobSchedulerQueueGroup group, int amount, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            requester = Guard.IsNotNull(requester);
            queueType = Guard.IsNotNullOrWhitespace(queueType);
            group = Guard.IsNotNull(group);
            amount = Guard.Is(amount, x => x >= 0);

            _logger.Log($"Starting request pipeline for scheduler <{requester.Name}> to request <{amount}> jobs from group with <{group.CurrentActiveQueues.Count}> active queues of type <{queueType}>");

            if (!group.Middleware.HasValue())
            {
                _logger.Debug($"No middleware defined for scheduler <{requester.Name}>. Calling job queue directly");

                await foreach(var job in Queue.DequeueAsync(queueType, group.CurrentActiveQueues.Select(x => x.Name), amount, cancellationToken))
                {
                    yield return job;
                    if (cancellationToken.IsCancellationRequested) yield break;
                }
            }
            else
            {
                _logger.Log($"Calling request pipeline with <{group.Middleware!.Count}> middleware for scheduler <{requester.Name}> to request <{amount}> jobs from group with <{group.CurrentActiveQueues.Count}> active queues of type <{queueType}>");
                Func<IJobScheduler, IEnumerable<IJobSchedulerQueueState>, int, CancellationToken, IAsyncEnumerable<IDequeuedJob>> next;
                int currentIndex = 0;
                next = (currentRequester, currentQueues, currentAmount, currentCancellationToken) =>
                {
                    return CallPipeline(group.Middleware, group.Middleware.Keys.OrderByDescending(x => x.Priority.HasValue).OrderBy(x => x.Priority).ToList(), requester, group, queueType, group.CurrentActiveQueues, amount, cancellationToken, currentIndex);
                };

                await foreach(var job in next(requester, group.CurrentActiveQueues, amount, cancellationToken))
                {
                    yield return job;
                    if (cancellationToken.IsCancellationRequested) yield break;
                }
            }
        }

        private async IAsyncEnumerable<IDequeuedJob> CallPipeline(IReadOnlyDictionary<IJobSchedulerMiddleware, object?> middleware, IReadOnlyList<IJobSchedulerMiddleware> orderedMiddleware, IJobScheduler requester, IJobSchedulerQueueGroup group, string queueType, IEnumerable<IJobSchedulerQueueState> queues, int amount, [EnumeratorCancellation] CancellationToken cancellationToken, int currentIndex = 0)
        {
            middleware = Guard.IsNotNullOrEmpty(middleware);
            orderedMiddleware = Guard.IsNotNullOrEmpty(orderedMiddleware);
            requester = Guard.IsNotNull(requester);
            group = Guard.IsNotNull(group);
            queueType = Guard.IsNotNullOrWhitespace(queueType);
            queues = Guard.IsNotNull(queues);
            amount = Guard.Is(amount, x => x >= 0);

            if (currentIndex >= middleware.Count)
            {
                _logger.Debug($"At the end of the request pipeline for scheduler <{requester.Name}>. Calling job queue");

                await foreach (var job in Queue.DequeueAsync(queueType, queues.Where(x => x.IsActive).Select(x => x.Name), amount, cancellationToken))
                {
                    yield return job;
                    if (cancellationToken.IsCancellationRequested) yield break;
                }
            }
            else
            {
                var currentMiddleware = orderedMiddleware[currentIndex];
                currentIndex++;

                _logger.Log($"Calling middleware <{currentMiddleware.GetType().Name}> for scheduler <{requester.Name}> to request <{amount}> jobs from <{queues.GetCount()}> queue of type <{queueType}>");

                var next = new Func<IJobScheduler, IEnumerable<IJobSchedulerQueueState>, int, CancellationToken, IAsyncEnumerable<IDequeuedJob>>((currentRequester, currentQueues, currentAmount, currentCancellationToken) =>
                {
                    return CallPipeline(middleware, orderedMiddleware, currentRequester, group, queueType, currentQueues, currentAmount, currentCancellationToken, currentIndex);
                });

                await foreach(var job in currentMiddleware.RequestAsync(requester, group, queueType, queues, amount, middleware[currentMiddleware], next, cancellationToken))
                {
                    yield return job;
                    if (cancellationToken.IsCancellationRequested) yield break;
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            await using var asyncLock = await AsyncLock.LockAsync().ConfigureAwait(false);
            if (IsDisposed.HasValue) return;
            using (new InProcessAction(x => IsDisposed = x))
            {
                await _ownedMiddleware.ForceExecuteAsync(x => x.DisposeAsync().AsTask(), (x, e) => _logger.Log($"Could not properly dispose middleware <{x}>", e)).ConfigureAwait(false);
            }
        }
    }
}
