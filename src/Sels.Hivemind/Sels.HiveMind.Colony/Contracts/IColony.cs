using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Represents a colony of <see cref="IDaemon"/>(s) connected to a HiveMind environment.
    /// </summary>
    public interface IColony : IColonyConfigurator, IReadOnlyColony, IAsyncDisposable
    {
        // Properties
        /// <summary>
        /// The daemons managed by the colony.
        /// </summary>
        new IReadOnlyList<IDaemon> Daemons { get; }

        /// <summary>
        /// Starts the colony.
        /// </summary>
        /// <param name="token">Optional token to cancel the start if the start hasn't be initiated yet. Once running the token won't have an effect on the state</param>
        /// <returns>Task containing the execution state</returns>
        Task StartAsync(CancellationToken token = default);

        /// <summary>
        /// Stops the colony.
        /// </summary>
        /// <param name="token">Optional token to cancel the stop if the stop hasn't be initiated yet</param>
        /// <returns>Task containing the execution state</returns>
        Task StopAsync(CancellationToken token = default);
    }
}
