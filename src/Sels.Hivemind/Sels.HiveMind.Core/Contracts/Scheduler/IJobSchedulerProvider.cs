using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Provider that is able to create <see cref="IJobScheduler"/>(s) of a certain type.
    /// </summary>
    public interface IJobSchedulerProvider
    {
        /// <summary>
        /// Creates a new scheduler of type <paramref name="type"/> with name <paramref name="name"/> using the provided settings.
        /// </summary>
        /// <param name="type">The type of the scheduler to create</param>
        /// <param name="name">The name of the scheduler to create, mainly used to get configuration</param>
        /// <param name="queueType">The type of queue the scheduler should fetch jobs from</param>
        /// <param name="queueGroups">The queues the scheduler should fetch jobs from. Queues are grouped by priority. The queue groups returned first should get priority over groups returned later</param>
        /// <param name="levelOfConcurrency">The maximum amount of concurrent calls the scheduler should optimise for</param>
        /// <param name="queue">The job queue the scheduler should use to fetch jobs from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new <see cref="IJobScheduler"/> component of type <paramref name="type"/> with name <paramref name="name"/></returns>
        public Task<IComponent<IJobScheduler>> CreateSchedulerAsync(string type, string name, string queueType, IEnumerable<IEnumerable<string>> queueGroups, int levelOfConcurrency, IJobQueue queue, CancellationToken token = default);
    }
}
