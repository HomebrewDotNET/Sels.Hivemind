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
        Task<string> CreateJobAsync(JobStorageData jobData, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Updates a job in the storage.
        /// </summary>
        /// <param name="jobData">The data of the job to update</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The generated id for the job</returns>
        Task<string> UpdateJobAsync(JobStorageData jobData, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Removes a job by id if the job is not locked.
        /// </summary>
        /// <param name="id">The id of the job to delete</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was deleted, otherwise false</returns>
        Task<bool> DeleteJobAsync(string id, IStorageConnection connection, CancellationToken token = default);
    }
}
