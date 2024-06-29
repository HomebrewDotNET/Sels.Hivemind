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
        /// <param name="configuration">The configuration for the scheduler</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new <see cref="IJobScheduler"/> component of type <paramref name="type"/> with name <paramref name="name"/></returns>
        public Task<IComponent<IJobScheduler>> CreateSchedulerAsync(string type, JobSchedulerConfiguration configuration, CancellationToken token = default);
    }
}
