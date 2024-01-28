using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Factory that can create configured instances of <see cref="IJobScheduler"/>.
    /// </summary>
    public interface IJobSchedulerFactory
    {
        /// <summary>
        /// The type of the schedulers that the factory can create.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Creates a new scheduler of type <see cref="Type"/> with name <paramref name="name"/> using the provided settings.
        /// </summary>
        /// <param name="serviceProvider">Service provider that can be used to resolve any dependencies. Scope is managed by the caller</param>
        /// <param name="name">The name of the scheduler, mainly used to get configuration</param>
        /// <param name="queueType">The type of queue the scheduler should fetch jobs from</param>
        /// <param name="queueGroups">The queues the scheduler should fetch jobs from. Queues are grouped by priority. The queue groups returned first should get priority over groups returned later</param>
        /// <param name="levelOfConcurrency">The maximum amount of concurrent calls the scheduler should optimise for</param>
        /// <param name="queue">The job queue the scheduler should use to fetch jobs from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new configured scheduler of type <see cref="Type"/> with name <paramref name="name"/></returns>
        public Task<IJobScheduler> CreateSchedulerAsync(IServiceProvider serviceProvider, string name, string queueType, IEnumerable<IEnumerable<string>> queueGroups, int levelOfConcurrency, IJobQueue queue, CancellationToken token =  default);  
    }
}
