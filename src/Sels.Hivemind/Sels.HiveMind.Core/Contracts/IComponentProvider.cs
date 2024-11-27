using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// Provider that is able to resolved named components of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of component produced</typeparam>
    public interface IComponentProvider<T> where T : class
    {
        /// <summary>
        /// Creates a new component instance with name <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the component implementation to resolve</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new component instance with name <paramref name="name"/></returns>
        Task<IComponent<T>> CreateAsync(string name, CancellationToken token = default);
    }
    /// <summary>
    /// Provider that is able to resolved named components of <typeparamref name="T"/> using argument of type <typeparamref name="TArgs"/>.
    /// </summary>
    /// <typeparam name="T">The type of component produced</typeparam>
    /// <typeparam name="TArgs">The type of arguments that is used to resolve a component</typeparam>
    public interface IComponentProvider<T, TArgs> where T : class
    {
        /// <summary>
        /// Creates a new component instance with name <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the component implementation to resolve</param>
        /// <param name="args">The arguments to use to create the new components</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new component instance with name <paramref name="name"/></returns>
        Task<IComponent<T>> CreateAsync(string name, TArgs args, CancellationToken token = default);
    }
}
