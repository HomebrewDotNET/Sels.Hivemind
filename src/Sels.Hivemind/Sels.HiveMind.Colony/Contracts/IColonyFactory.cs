using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Factory that can be used to create new <see cref="IColony"/>(s)
    /// </summary>
    public interface IColonyFactory
    {
        /// <summary>
        /// Creates a new <see cref="IColony"/>.
        /// </summary>
        /// <param name="builder">Delegate used to configure the new <see cref="IColony"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new <see cref="IColony"/> configured by <paramref name="builder"/></returns>
        Task<IColony> CreateAsync(Action<IColonyBuilder> builder, CancellationToken token = default);
    }
}
