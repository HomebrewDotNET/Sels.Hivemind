using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Creates instances from various types.
    /// </summary>
    public interface IActivator
    {
        /// <summary>
        /// Creates a new scope to active services.
        /// </summary>
        /// <param name="serviceProvider">Service provider scope opened by the caller</param>
        /// <returns>A new scope to active services</returns>
        public Task<IActivatorScope> CreateActivatorScopeAsync(IServiceProvider serviceProvider);
    }

    /// <summary>
    /// Activator scope where instances created by the scope share the same lifetime as the scope itself.
    /// </summary>
    public interface IActivatorScope : IAsyncDisposable
    {
        /// <summary>
        /// Creates an instance of <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The type to activate</param>
        /// <returns>An instance of <paramref name="type"/></returns>
        Task<object> ActivateAsync(Type type);
    }
}
