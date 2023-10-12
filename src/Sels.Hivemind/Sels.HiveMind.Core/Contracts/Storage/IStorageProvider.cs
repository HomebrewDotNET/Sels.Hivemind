using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Provider access to <see cref="IStorage"/> configured for a certain HiveMind environment.
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>
        /// Gets a <see cref="IStorage"/> for interacting with storage in environment with name <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The name of the environment to get the storage for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A <see cref="IStorage"/> component for interacting with storage in environment with name <paramref name="environment"/></returns>
        public Task<IEnvironmentComponent<IStorage>> GetStorageAsync(string environment, CancellationToken token = default);
    }
}
