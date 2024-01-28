using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Factory for creating storage instances configured for a certain HiveMind environment.
    /// </summary>
    public interface IStorageFactory
    {
        // Property
        /// <summary>
        /// The environment the factory can create storages for.
        /// </summary>
        public string Environment { get; }

        /// <summary>
        /// Creates a new storage instance for <see cref="Environment"/>.
        /// </summary>
        /// <param name="serviceProvider">Service provider that can be used to resolve dependencies. Scope is managed by caller</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Storage configured for <see cref="Environment"/></returns>
        Task<IStorage> CreateStorageAsync(IServiceProvider serviceProvider, CancellationToken token = default);
    }
}
