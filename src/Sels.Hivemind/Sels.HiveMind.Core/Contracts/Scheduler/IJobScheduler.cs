using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Responsible for scheduling <see cref="IDequeuedJob"/>(s) on requesting consumers.
    /// </summary>
    public interface IJobScheduler
    {
        /// <summary>
        /// The name assigned to this scheduler.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The type of queue the scheduler will fetch work from.
        /// </summary>
        public string QueueType { get; }
        /// <summary>
        /// The queues the scheduler should fetch work from. Queues are grouped by priority. The queue groups earlier in the list should get priority over groups later in the list.
        /// </summary>
        public IReadOnlyList<IReadOnlyCollection<string>> QueueGroups { get; }
        /// <summary>
        /// The queue that the scheduler uses to dequeue jobs.
        /// </summary>
        public IJobQueue Queue { get; }
        /// <summary>
        /// How many concurrent requesters the scheduler is optimised for. Going above this limit might not yield any performance benefits.
        /// </summary>
        public int LevelOfConcurrency { get; }

        /// <summary>
        /// Requests the next available <see cref="IDequeuedJob"/>. If nothing is avaiable the method should block asynchronously.
        /// </summary>
        /// <param name="token">Token that will be used to either cancel the dequeue or the wait</param>
        /// <returns>The next available job</returns>
        public Task<IDequeuedJob> RequestAsync(CancellationToken token);
    }
}
