using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Contains the configuration options of a middleware + how to construct the middleware.
    /// </summary>
    /// <typeparam name="T">The interface type of the middleware</typeparam>
    public interface ISwarmHostMiddlewareOptions<T> where T : class
    {
        /// <inheritdoc cref="ISwarmHostMiddlewareConfigurationOptions"/>
        public ISwarmHostMiddlewareConfigurationOptions ConfigurationOptions { get; }
        /// <summary>
        /// Contains the middleware storage data that is used to construct the middleware.
        /// Mutually exclusive with <see cref="Factory"/>.
        /// </summary>
        public IMiddlewareStorageData? Data { get; }
        /// <summary>
        /// The delegate that will be invoked to construct the middleware.
        /// </summary>
        public Func<IServiceProvider, Task<IComponent<T>>>? Factory { get; }
        /// <summary>
        /// The context for the middleware that will be used when <see cref="Factory"/> is set.
        /// </summary>
        public object? Context { get; }
    }
}
