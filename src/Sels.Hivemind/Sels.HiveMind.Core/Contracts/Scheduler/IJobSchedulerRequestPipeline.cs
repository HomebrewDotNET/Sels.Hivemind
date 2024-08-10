using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// A pipeline that is used by a <see cref="IJobScheduler"/> to request jobs to process.
    /// </summary>
    public interface IJobSchedulerRequestPipeline : IAsyncDisposable
    {
        /// <summary>
        /// Lock that can be used to synchronize access to the pipeline.
        /// </summary>
        public SemaphoreSlim AsyncLock { get; }
        /// <summary>
        /// The backing job queue that is used by the pipeline to fetch jobs.
        /// </summary>
        public IJobQueue Queue { get; }

        /// <summary>
        /// Returns an enumerator that can return at least <paramref name="amount"/> jobs.
        /// </summary>
        /// <param name="requester">The scheduler who is requesting the jobs</param>
        /// <param name="queueType">The type of queue to fetch jobs from</param>
        /// <param name="group">The group to fetch jobs from</param>
        /// <param name="amount">How many jobs <paramref name="requester"/> is requesting</param>
        /// <param name="cancellationToken">Token that can be used to either cancel the request or the returned enumerator</param>
        /// <returns>An enumerator that can return at least <paramref name="amount"/> jobs</returns>
        public IAsyncEnumerable<IDequeuedJob> RequestAsync(IJobScheduler requester, string queueType, IJobSchedulerQueueGroup group, int amount, CancellationToken cancellationToken = default);
    }
}
