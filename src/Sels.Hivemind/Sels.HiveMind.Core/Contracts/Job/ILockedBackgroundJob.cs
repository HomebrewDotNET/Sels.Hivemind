using Sels.Core.Async.TaskManagement;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;
using static Sels.Core.Helper;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a locked background where the state can be modified. Disposing the job will release the lock if one is still set.
    /// </summary>
    public interface ILockedBackgroundJob : IWriteableBackgroundJob
    {
        /// <summary>
        /// Tries to update the heartbeat on the lock of the job.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the heartbeat was set, otherwise false</returns>
        public Task<bool> SetHeartbeatAsync(CancellationToken token = default);
        /// <summary>
        /// Saves any changes made to the background job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(IClientConnection connection, bool retainLock, CancellationToken token = default);
        /// <summary>
        /// Saves any changes made to the background job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(IClientConnection connection, CancellationToken token = default) => SaveChangesAsync(connection, false, token);
        /// <summary>
        /// Saves any changes made to the background job.
        /// </summary>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(bool retainLock, CancellationToken token = default);
        /// <summary>
        /// Saves any changes made to the background job. Lock will be released after saving.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(CancellationToken token = default) => SaveChangesAsync(false, token);      
    }
}
