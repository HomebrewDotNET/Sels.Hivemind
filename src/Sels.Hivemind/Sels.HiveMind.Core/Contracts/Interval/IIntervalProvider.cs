using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Interval
{
    /// <summary>
    /// Provides access to <see cref="IInterval"/> of certain types.
    /// </summary>
    public interface IIntervalProvider
    {
        /// <summary>
        /// Gets a new instance of <see cref="IInterval"/> of type <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The type of <see cref="IInterval"/> to create an instance for</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new <see cref="IInterval"/> component of type <paramref name="type"/></returns>
        public Task<IComponent<IInterval>> GetIntervalAsync(string type, CancellationToken token = default);
    }
}
