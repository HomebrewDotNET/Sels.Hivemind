using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Factory for creating queue instances configured for a certain HiveMind environment.
    /// </summary>
    public interface IJobQueueFactory
    {
        // Property
        /// <summary>
        /// The environment the factory can create queues for.
        /// </summary>
        public string Environment { get; }

        /// <summary>
        /// Creates a new queue instance for <see cref="Environment"/>.
        /// </summary>
        /// <param name="serviceProvider">Service provider that can be used to resolve dependencies. Scope is managed by caller</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Queue configured for <see cref="Environment"/></returns>
        Task<IJobQueue> CreateQueueAsync(IServiceProvider serviceProvider, CancellationToken token = default);
    }
}
