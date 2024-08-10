using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <inheritdoc cref="ISwarmHostMiddlewareOptions{T}"/>
    public class SwarmHostMiddlewareOptions<T> : ISwarmHostMiddlewareOptions<T> where T : class
    {
        /// <inheritdoc/>
        ISwarmHostMiddlewareConfigurationOptions ISwarmHostMiddlewareOptions<T>.ConfigurationOptions => ConfigurationOptions;
        /// <inheritdoc cref="ISwarmHostMiddlewareConfigurationOptions"/>
        public SwarmHostMiddlewareConfigurationOptions ConfigurationOptions { get; set; } = new SwarmHostMiddlewareConfigurationOptions();
        /// <inheritdoc/>
        IMiddlewareStorageData? ISwarmHostMiddlewareOptions<T>.Data => Data;
        /// <inheritdoc cref="IMiddlewareStorageData"/>
        public MiddlewareStorageData? Data { get; set; }
        /// <inheritdoc/>
        public Func<IServiceProvider, Task<IComponent<T>>>? Factory { get; set; }
        /// <inheritdoc/>
        public object? Context { get; set; }
    }
}
