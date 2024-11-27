using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <inheritdoc cref="IJobSchedulerQueueState"/>
    public class JobSchedulerQueueState : IJobSchedulerQueueState
    {
        // Fields
        private readonly object _lock = new object();

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public DateTime ActiveAfter { get; set; }
        /// <inheritdoc/>
        public DateTime LastActive { get; private set; }
        /// <inheritdoc/>
        public long Retrieved { get; private set; }
        /// <inheritdoc/>
        public ConcurrentDictionary<string, object> Tags { get; } = new ConcurrentDictionary<string, object>();
        /// <inheritdoc/>
        public DateTime LastEmpty { get; set; }

        /// <inheritdoc cref="JobSchedulerQueueState"/>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        public JobSchedulerQueueState(string name)
        {
            Name = Guard.IsNotNullOrWhitespace(name);
        }

        /// <inheritdoc/>
        public void SetRetrieved(int amount)
        {
            amount = Guard.IsLarger(amount, 0);

            var now = DateTime.Now;

            lock (_lock)
            {
               LastActive = now;
                Retrieved += amount;
            }
        }
    }
}
