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
        /// <param name="configuration">The configuration for the scheduler</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new configured scheduler of type <see cref="Type"/> configured using <paramref name="configuration"/></returns>
        public Task<IJobScheduler> CreateSchedulerAsync(IServiceProvider serviceProvider, JobSchedulerConfiguration configuration, CancellationToken token = default);
    }
}
