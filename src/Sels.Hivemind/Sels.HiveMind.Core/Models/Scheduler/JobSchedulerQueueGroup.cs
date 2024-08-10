using Sels.Core.Extensions.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <inheritdoc cref="IJobSchedulerQueueGroup"/>
    public class JobSchedulerQueueGroup : IJobSchedulerQueueGroup
    {
        // Fields
        private readonly object _lock = new object();
        private readonly List<IJobSchedulerQueueState> _currentQueues = new List<IJobSchedulerQueueState>();

        // Properties
        /// <inheritdoc/>
        public IReadOnlyList<string> SourceQueues { get; }
        /// <inheritdoc/>
        public IReadOnlyList<IJobSchedulerQueueState> CurrentQueues
        {
            get
            {
                lock (_currentQueues)
                {
                    return _currentQueues.ToList();
                }
            }
        }
        /// <inheritdoc/>
        public IReadOnlyList<IJobSchedulerQueueState> CurrentActiveQueues
        {
            get
            {
                lock (_currentQueues)
                {
                    return _currentQueues.Where(x =>
                    {
                        lock (x)
                        {
                            return x.IsActive;
                        }
                    }).ToList();
                }
            }
        }
        /// <inheritdoc/>
        public byte Priority { get; internal set; }
        /// <inheritdoc/>
        public ConcurrentDictionary<string, object?> Tags { get; } = new ConcurrentDictionary<string, object?>();
        /// <inheritdoc/>
        public IReadOnlyDictionary<IJobSchedulerMiddleware, object?>? Middleware { get; }

        /// <inheritdoc cref="JobSchedulerQueueGroup"/>
        /// <param name="sourceQueues"><inheritdoc cref="SourceQueues"/></param>
        /// <param name="middleware"><inheritdoc cref="Middleware"/></param>
        public JobSchedulerQueueGroup(IEnumerable<string> sourceQueues, IEnumerable<(IJobSchedulerMiddleware Middleware, object? Context)>? middleware)
        {
            SourceQueues = Guard.IsNotNull(sourceQueues).Distinct().ToList();
            sourceQueues.Execute(x => AddQueue(x));
            if(middleware != null) Middleware = middleware.ToDictionary(x => x.Middleware, x => x.Context);
        }

        /// <inheritdoc/>
        public void AddQueue(string queue)
        {
            queue = Guard.IsNotNullOrWhitespace(queue);

            lock(_currentQueues)
            {
                if(!_currentQueues.Any(x => x.Name.Equals(queue, StringComparison.OrdinalIgnoreCase)))
                {
                    _currentQueues.Add(new JobSchedulerQueueState(queue));
                }
            }
        }
        /// <inheritdoc/>
        public void RemoveQueue(string queue)
        {
            queue = Guard.IsNotNullOrWhitespace(queue);

            lock (_currentQueues)
            {
                var existingQueue = _currentQueues.FirstOrDefault(x => x.Name.Equals(queue, StringComparison.OrdinalIgnoreCase));
                if (existingQueue != null)
                {
                    _currentQueues.Remove(existingQueue);
                }
            }
        }
    }
}
