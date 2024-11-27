using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Contains the state of the queues used by the job scheduler to retrieve jobs.
    /// </summary>
    public interface IJobSchedulerQueues
    {

        /// <summary>
        /// The current queues the job scheduler will use.
        /// </summary>
        public IReadOnlyList<IJobSchedulerQueueGroup> CurrentQueueGroups { get; }

        /// <summary>
        /// Returns the active queue groups by priority.
        /// </summary>
        /// <returns>An enumerator that returns all active queue groups</returns>
        public IEnumerable<IJobSchedulerQueueGroup> GetActiveQueueGroups();

        /// <summary>
        /// Adds a new queue to <see cref="CurrentQueueGroups"/>.
        /// </summary>
        /// <param name="queue">The name of the queue to add</param>
        /// <param name="priority">The priority of the queue. Will be used to determine the queue group to insert to. Lower vlaue means higher priority. Null means new group at the tail</param>
        public void AddQueue(string queue, byte? priority = null);
        /// <summary>
        /// Removes a queue from <see cref="CurrentQueueGroups"/>.
        /// </summary>
        /// <param name="queue">The name of the queue to remove</param>
        public void RemoveQueue(string queue);

        /// <summary>
        /// Tags that can be set on the scheduler to pass around state.
        /// </summary>
        public ConcurrentDictionary<string, object> Tags { get; }
    }
}
