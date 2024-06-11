using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a locked background job where the state can be modified and saved or where the job can be deleted. Disposing the job will release the lock if one is still set.
    /// </summary>
    public interface ILockedBackgroundJob : ILockedJob<ILockedBackgroundJob, IBackgroundJobChangeTracker, IBackgroundJobState, IBackgroundJobAction>, IWriteableBackgroundJob
    {
        /// <summary>
        /// Saves any changes made to the background job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(IStorageConnection connection, bool retainLock, CancellationToken token = default);
        /// <summary>
        /// Saves any changes made to the background job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(IClientConnection connection, bool retainLock, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            return SaveChangesAsync(connection.StorageConnection, retainLock, token);
        }
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
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task SaveChangesAsync(IStorageConnection connection, CancellationToken token = default) => SaveChangesAsync(connection, false, token);
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


        /// <summary>
        /// Tries to set the background job to the <see cref="SystemDeletedState"/>. Used when deletion of the job was triggered outside of the job (Like when bulk deleting)
        /// </summary>
        /// <param name="connection">The connection that was used to delete the job</param>
        /// <param name="reason">The reason for the deletion</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the state change was successful, otherwise false</returns>
        Task<bool> SetSystemDeletedAsync(IStorageConnection connection, string reason = null, CancellationToken token = default);
    }
}
