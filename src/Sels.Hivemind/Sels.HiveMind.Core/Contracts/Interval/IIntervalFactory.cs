using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Interval
{
    /// <summary>
    /// Factory that is able to create instances of <see cref="IInterval"/> of a certain type.
    /// </summary>
    public interface IIntervalFactory
    {
        /// <summary>
        /// The type of interval that this factory can create.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Creates an instance of <see cref="IInterval"/> of type <see cref="Type"/>.
        /// </summary>
        /// <param name="serviceProvider">Service provider that can be used to resolve dependencies. Scope is managed by caller</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new instance of <see cref="IInterval"/> of type <see cref="Type"/></returns>
        Task<IInterval> CreateIntervalAsync(IServiceProvider serviceProvider, CancellationToken token = default);
    }
}
