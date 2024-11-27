using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Middleware that sits between a <see cref="IJobScheduler"/> and <see cref="IJobQueue"/> and can be used to intercept job requests.
    /// </summary>
    public interface IJobSchedulerMiddleware : IMiddleware
    {
        /// <summary>
        /// Executes the middleware when <paramref name="requester"/> requests jobs.
        /// </summary>
        /// <param name="requester">The scheduler who is requesting the jobs</param>
        /// <param name="group">The group the current middleware is attached to</param>
        /// <param name="queueType">THe type of queue to fetch jobs from</param>
        /// <param name="queues">The queues to fetch jobs from</param>
        /// <param name="amount">How many jobs <paramref name="requester"/> or any previous middleware is requesting</param>
        /// <param name="context">Optional context that acts as input for the middleware</param>
        /// <param name="next">Delegate that calls the next middleware or job queue in the chain. Doesn't have to be called</param>
        /// <param name="cancellationToken">Token that can be used to either cancel the request or the returned enumerator</param>
        /// <returns>An enumerator that can return at least <paramref name="amount"/> jobs</returns>
        public IAsyncEnumerable<IDequeuedJob> RequestAsync(IJobScheduler requester, IJobSchedulerQueueGroup group, string queueType, IEnumerable<IJobSchedulerQueueState> queues, int amount, object? context, Func<IJobScheduler, IEnumerable<IJobSchedulerQueueState>, int, CancellationToken, IAsyncEnumerable<IDequeuedJob>> next, CancellationToken cancellationToken);
    }
}
