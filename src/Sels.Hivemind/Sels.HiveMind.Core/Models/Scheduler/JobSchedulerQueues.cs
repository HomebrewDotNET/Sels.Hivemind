using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <inheritdoc cref="IJobSchedulerQueues"/>
    public class JobSchedulerQueues : IJobSchedulerQueues
    {
        // Fields
        private readonly List<IJobSchedulerQueueGroup> _currentGroups = new List<IJobSchedulerQueueGroup>();
        // Properties
        /// <inheritdoc/>
        public ConcurrentDictionary<string, object> Tags { get; } = new ConcurrentDictionary<string, object>();
        /// <inheritdoc/>
        public IReadOnlyList<IJobSchedulerQueueGroup> CurrentQueueGroups
        {
            get
            {
                lock (_currentGroups)
                {
                    return _currentGroups.OrderBy(x => x.Priority).ToList();
                }
            }
        }

        /// <inheritdoc cref="JobSchedulerQueues"/>
        /// <param name="queueGroups"><inheritdoc cref="CurrentQueueGroups"/></param>
        public JobSchedulerQueues(IEnumerable<IJobSchedulerQueueGroup> queueGroups)
        {
            queueGroups = Guard.IsNotNull(queueGroups);

            _currentGroups.AddRange(queueGroups);
        }

        /// <inheritdoc/>
        public IEnumerable<IJobSchedulerQueueGroup> GetActiveQueueGroups()
        {
            IJobSchedulerQueueGroup[] currentQueues;
            lock (_currentGroups)
            {
                currentQueues = _currentGroups.ToArray();
            }

            foreach (var group in currentQueues.Where(x => x.CurrentActiveQueues.HasValue()))
            {
                yield return group;
            }
        }
        /// <inheritdoc/>
        public void AddQueue(string queue, byte? priority = null)
        {
            queue = Guard.IsNotNull(queue);
            if(priority.HasValue) Guard.IsLargerOrEqual(priority.Value, 0);

            lock (_currentGroups)
            {
                IJobSchedulerQueueState? state = null;
                foreach(var group in _currentGroups)
                {
                    lock (group)
                    {
                        foreach (var existingQueue in group.CurrentQueues)
                        {
                            if (queue.Equals(existingQueue.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                state = existingQueue;
                                RemoveQueue(existingQueue.Name);
                                break;
                            }
                        }
                    }
                    if(state != null)
                    {
                        break;
                    }
                }

                state ??= new JobSchedulerQueueState(queue);

                bool wasAdded = false;
                if (priority.HasValue)
                {
                    var existingQueue = _currentGroups.FirstOrDefault(x => x.Priority == priority.Value);

                    if(existingQueue != null)
                    {
                        existingQueue.AddQueue(queue);
                        wasAdded = true;
                    }
                }
                else
                {
                    priority = (byte?)((int)_currentGroups.Max(x => x.Priority) + 1);
                }

                if (!wasAdded)
                {
                    if (!_currentGroups.HasValue())
                    {
                        var group = new JobSchedulerQueueGroup(Array.Empty<string>(), null);
                        group.Priority = (byte)priority.Value;
                        group.AddQueue(queue);
                        _currentGroups.Add(group);
                    }
                }
            }          
        }
        /// <inheritdoc/>
        public void RemoveQueue(string queue)
        {
            queue = Guard.IsNotNull(queue);

            lock (_currentGroups)
            {
                foreach (var group in _currentGroups.Where(x => x.CurrentQueues.Any(x => x.Name.Equals(queue, StringComparison.OrdinalIgnoreCase))))
                {
                    group.RemoveQueue(queue);
                }
            }
        }
    }
}
