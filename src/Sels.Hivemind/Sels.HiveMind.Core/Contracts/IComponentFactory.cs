using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// Factory that is able to create new instance of <typeparamref name="T"/> using name <see cref="Name"/>.
    /// </summary>
    /// <typeparam name="T">The type that the factory can create</typeparam>
    public interface IComponentFactory<T>
    {
        /// <summary>
        /// The name of the implementation that this factory can produce.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates a new instance of <typeparamref name="T"/> with implementation name <see cref="Name"/>.
        /// </summary>
        /// <param name="scope">Service scope that can be used to resolve any dependencies. Scope itself is managed by the caller</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new instance of <typeparamref name="T"/> with implemntation name <see cref="Name"/></returns>
        Task<T> CreateAsync(IServiceProvider scope, CancellationToken token = default);
    }

    /// <summary>
    /// Factory that is able to create new instance of <typeparamref name="T"/> using name <see cref="Name"/>.
    /// </summary>
    /// <typeparam name="T">The type that the factory can create</typeparam>
    /// <typeparam name="TArgs">The type of arguments that is used to create a new instance of <typeparamref name="T"/></typeparam></typeparam>
    public interface IComponentFactory<T, TArgs>
    {
        /// <summary>
        /// The name of the implementation that this factory can produce.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates a new instance of <typeparamref name="T"/> with implementation name <see cref="Name"/>.
        /// </summary>
        /// <param name="scope">Service scope that can be used to resolve any dependencies. Scope itself is managed by the caller</param>
        /// <param name="args">The arguments to use to create the new instance</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new instance of <typeparamref name="T"/> with implemntation name <see cref="Name"/></returns>
        Task<T> CreateAsync(IServiceProvider scope, TArgs args, CancellationToken token = default);
    }
}
