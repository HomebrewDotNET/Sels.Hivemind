using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Provides access to <see cref="IJobQueue"/> configured for a certain HiveMind environment.
    /// </summary>
    public interface IJobQueueProvider
    {
        /// <summary>
        /// Gets a <see cref="IJobQueue"/> for interacting with queues in environment with name <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The name of the environment to get the queue for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A <see cref="IJobQueue"/> component for interacting with queues in environment with name <paramref name="environment"/></returns>
        public Task<IEnvironmentComponent<IJobQueue>> GetQueueAsync(string environment, CancellationToken token = default);
    }
}
