using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Storage for managing state.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Opens a new connection for the current environment.
        /// </summary>
        /// <param name="startTransaction">True if a transaction should be started for this connection, otherwise false if a transaction isn't needed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An open connection to be used with the current environment</returns>
        Task<IStorageConnection> OpenConnectionAsync(bool startTransaction, CancellationToken token = default);

        /// <summary>
        /// Stores a new job in the storage.
        /// </summary>
        /// <param name="jobData">The data of the job to store</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The generated id for the job</returns>
        Task<string> CreateBackgroundJobAsync(JobStorageData jobData, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of background job <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The latest state of background job <paramref name="id"/> or null if the job doesn't exist</returns>
        Task<JobStorageData> GetBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Tries to acquire an exclusive lock on background job <paramref name="id"/> for <paramref name="requester"/>.
        /// </summary>
        /// <param name="id">The id of the background job to lock</param>
        /// <param name="requester">Who is requesting the lock. Should only acquire the lock if the job is not locked or already locked by <paramref name="requester"/></param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The current lock state of the lock regardless if locking was successful or not, otherwise null if the job doesn't exist</returns>
        Task<LockStorageData> TryLockBackgroundJobAsync(string id, string requester, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Tries to keep the lock on background job <paramref name="id"/> by <paramref name="holder"/> alive by extending the heartbeat.
        /// </summary>
        /// <param name="id">The id of the job to set the heartbeat on</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The current state of the lock regardless if the heartbeat was extended or not, otherwise null if the job doesn't exist</returns>
        Task<LockStorageData> TryHeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Tries to release the lock on background job <paramref name="id"/> if it is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="id">The id of the job to unlock</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the lock was released, otherwise false</returns>
        Task<bool> UnlockBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Updates a job in the storage. Should check lock ownership.
        /// </summary>
        /// <param name="jobData">The data of the job to update</param>
        /// <param name="releaseLock">If the lock has to be removed</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the update was successful, otherwise false</returns>
        Task<bool> UpdateBackgroundJobAsync(JobStorageData jobData, IStorageConnection connection, bool releaseLock, CancellationToken token = default);
        /// <summary>
        /// Removes a job by id if the job is not locked.
        /// </summary>
        /// <param name="id">The id of the job to delete</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was deleted, otherwise false</returns>
        Task<bool> DeleteBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default);
    }
}
