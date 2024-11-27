using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Contains common methods used by all HiveMind clients.
    /// </summary>
    public interface IClient
    {
        /// <summary>
        /// Opens a new connection for HiveMind environment <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The environment to open the connection for</param>
        /// <param name="startTransaction">True if a transaction should be started for this connection, otherwise false if a transaction isn't needed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An open connection to be used with the current environment</returns>
        Task<IClientConnection> OpenConnectionAsync([Traceable(HiveLog.Environment)] string environment, bool startTransaction = true, CancellationToken token = default);
        /// <summary>
        /// Opens a new connection for HiveMind environment <see cref="HiveMindConstants.DefaultEnvironmentName"/>
        /// </summary>
        /// <param name="startTransaction">True if a transaction should be started for this connection, otherwise false if a transaction isn't needed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An open connection to be used with the current environment</returns>
        Task<IClientConnection> OpenConnectionAsync(bool startTransaction = true, CancellationToken token = default) => OpenConnectionAsync(HiveMindConstants.DefaultEnvironmentName, startTransaction, token);
    }
}
