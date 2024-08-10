using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Contains the state of a queue used by a job scheduler.
    /// </summary>
    public interface IJobSchedulerQueueState
    {
        /// <summary>
        /// The name of the queue.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The date after which the queue becomes active and will be used by the scheduler to retrieve jobs.
        /// Can be set in the future to prevent the queue from being used until a certain date.
        /// </summary>
        public DateTime ActiveAfter { get; set; }
        /// <summary>
        /// The last time the queue was empty when it was checked by the scheduler.
        /// </summary>
        public DateTime LastEmpty { get; set; }
        /// <summary>
        /// The last time a job was retrieved from the queue.
        /// </summary>
        public DateTime LastActive { get; }
        /// <summary>
        /// How many jobs were retrieved from the queue.
        /// </summary>
        public long Retrieved { get; }
        /// <summary>
        /// If the current queue is currently activated and can be used by the scheduler.
        /// </summary>
        public bool IsActive => DateTime.Now >= ActiveAfter;
        /// <summary>
        /// True if the current queue could have jobs queued, otherwise false.
        /// </summary>
        public bool CouldHaveJobs => LastActive > LastEmpty;

        /// <summary>
        /// Indicates that <paramref name="amount"/> jobs were retrieved from the queue.
        /// </summary>
        /// <param name="amount">How many jobs were retrieved</param>
        public void SetRetrieved(int amount);

        /// <summary>
        /// Tags that can be set on the queue to pass around state.
        /// </summary>
        public ConcurrentDictionary<string, object> Tags { get; }
    }
}
