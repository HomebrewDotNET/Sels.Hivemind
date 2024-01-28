using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Helper interface for implementing daemons.
    /// </summary>
    public interface IDaemonExecutor
    {
        /// <summary>
        /// Executes the daemon until <paramref name="token"/> gets cancelled.
        /// </summary>
        /// <param name="context"><inheritdoc cref="IDaemonExecutionContext"/></param>
        /// <param name="token">Token that will get cancelled when the daemon is requested to stop running</param>
        /// <returns>Task that should complete as soon as possible when <paramref name="token"/> gets cancelled</returns>
        Task RunUntilCancellation(IDaemonExecutionContext context, CancellationToken token);
    }
}
