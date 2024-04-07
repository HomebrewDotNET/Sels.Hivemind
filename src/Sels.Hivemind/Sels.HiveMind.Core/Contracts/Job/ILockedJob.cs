using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.Core.Extensions;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a locked job. Disposing the job will release the lock if one is still set.
    /// </summary>
    /// <typeparam name="TLockedJob">The type of the locked job</typeparam>
    /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
    public interface ILockedJob<TLockedJob, TChangeTracker, TState, TAction> : IWriteableJob<TLockedJob, TChangeTracker, TState, TAction>
        where TState : IJobState
        where TChangeTracker : IJobChangeTracker<TState>
    {
        /// <summary>
        /// Tries to update the heartbeat on the lock of the job.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the heartbeat was set, otherwise false</returns>
        public Task<bool> SetHeartbeatAsync(CancellationToken token = default);
        
        // Deletion
        /// <summary>
        /// Permanently deletes the current job from the storage.
        /// This action can not be undone.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the delete with</param>
        /// <param name="reason">Optional reason that can be provided for the deletion</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the current job was permanently deleted, otherwise false</returns>
        Task<bool> SystemDeleteAsync(IClientConnection connection, string reason = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            return SystemDeleteAsync(connection, reason, token);
        }
        /// <summary>
        /// Permanently deletes the current job from the storage.
        /// This action can not be undone.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the delete with</param>
        /// <param name="reason">Optional reason that can be provided for the deletion</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task<bool> SystemDeleteAsync(IStorageConnection connection, string reason = null, CancellationToken token = default);
        /// <summary>
        /// Permanently deletes the current job from the storage.
        /// This action can not be undone.
        /// </summary>
        /// <param name="reason">Optional reason that can be provided for the deletion</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task<bool> SystemDeleteAsync(string reason = null, CancellationToken token = default);
    }
}
