using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Represents a group of queues that share the same priority used by a <see cref="IJobScheduler"/>.
    /// </summary>
    public interface IJobSchedulerQueueGroup
    {
        /// <summary>
        /// The initial queues that the group was created with.
        /// </summary>
        public IReadOnlyList<string> SourceQueues { get; }

        /// <summary>
        /// The current queues assigned to the group.
        /// Might not always be the same as <see cref="SourceQueues"/> depending on the middleware used.
        /// </summary>
        public IReadOnlyList<IJobSchedulerQueueState> CurrentQueues { get; }
        /// <summary>
        /// The current queues the job scheduler will use.
        /// Might not always be the same as <see cref="CurrentQueues"/> if some queues are disabled.
        /// </summary>
        public IReadOnlyList<IJobSchedulerQueueState> CurrentActiveQueues { get; }
        /// <summary>
        /// The middleware that will be used when fetching jobs from the current group.
        /// </summary>
        public IReadOnlyDictionary<IJobSchedulerMiddleware, object?>? Middleware { get; }

        /// <summary>
        /// The priority of the group. Lower value means higher priority.
        /// </summary>
        public byte Priority { get; }
        /// <summary>
        /// Adds a new queue to <see cref="CurrentQueues"/>.
        /// </summary>
        /// <param name="queue">The name of the queue to add</param>
        public void AddQueue(string queue);
        /// <summary>
        /// Removes a queue from <see cref="CurrentQueues"/>.
        /// </summary>
        /// <param name="queue">The name of the queue to remove</param>
        public void RemoveQueue(string queue);

        /// <summary>
        /// Tags that can be set on the queue group to pass around state.
        /// </summary>
        public ConcurrentDictionary<string, object?> Tags { get; }
    }
}
