using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Job;

namespace Sels.HiveMind.Service
{
    /// <summary>
    /// Contains common actions that can be performed on jobs.
    /// </summary>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TStateStorageData">The type of storage data used to store the job states</typeparam>
    /// <typeparam name="TStorageData">The type of storage data used by the job</typeparam>
    public interface IJobService<TStorageData, TState, TStateStorageData>
    {
        /// <summary>
        /// Keep the lock on job <paramref name="id"/> by <paramref name="holder"/> alive by extending the heartbeat.
        /// </summary>
        /// <param name="id">The id of the job to set the heartbeat on</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The current state of lock extended by <paramref name="holder"/></returns>
        /// <exception cref="JobNotFoundException"></exception>
        /// <exception cref="JobAlreadyLockedException"></exception>
        public Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of job <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="JobNotFoundException"></exception>
        /// <returns>The latest state of job <paramref name="id"/></returns>
        public Task<TStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of job <paramref name="id"/> if it exists optionally with a lock for <paramref name="requester"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="requester">Who is requesting the fetch</param>
        /// <param name="tryLock">True if the job should be lock by <paramref name="requester"/> if it exists and is not locked, otherwise not to attempt to lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="JobNotFoundException"></exception>
        /// <exception cref="JobAlreadyLockedException"></exception>
        /// <returns>WasLocked: true if the job was locked | Data: The latest state of the job if it exists</returns>
        public Task<(bool WasLocked, TStorageData Data)> FetchAsync(string id, IStorageConnection connection, string requester, bool tryLock, CancellationToken token = default);

        /// <summary>
        /// Fetches locked jobs where the last heartbeat on the lock was longer than the configured timeout for the HiveMind environment.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="limit">The maximum amount of jobs to return</param>
        /// <param name="requester">Who is requesting the locked jobs</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with the storage data of all timed out  jobs</returns>
        Task<TStorageData[]> GetTimedOutJobs(IStorageConnection connection, int limit, string requester, CancellationToken token = default);

        /// <summary>
        /// Creates <paramref name="action"/> in the storage and assigns a unique id to it.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="action">The action to create</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when <paramref name="action"/> is created</returns>
        Task CreateActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default);
        /// <summary>
        /// Fetches the next <paramref name="limit"/> actions defined for background job <paramref name="id"/> ordered by priority.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the background job to fetch the actions for</param>
        /// <param name="limit">The maximum amount of actions to return</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with actions defined for background job <paramref name="id"/> or an empty array when nothing is defined</returns>
        Task<ActionInfo[]> GetNextActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default);
        /// <summary>
        /// Attempts to delete background job action <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the action to delete</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if action <paramref name="id"/> was deleted, otherwise false</returns>
        Task<bool> DeleteActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default);

        /// <summary>
        /// Converts state in storage format back into it's original type.
        /// </summary>
        /// <param name="stateData">The state data</param>
        /// <param name="environment">The HiveMind environment <paramref name="stateData"/> is from</param>
        /// <returns>State instance created from <paramref name="stateData"/></returns>
        public TState ConvertToState(TStateStorageData stateData, string environment);
        /// <summary>
        /// Gets all the properties to store for <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to get the properties for</param>
        /// <param name="environment">The HiveMind environment <paramref name="state"/> is from</param>
        /// <returns>All the state properties to store for <paramref name="state"/> if there are any</returns>
        public IEnumerable<StorageProperty> GetStorageProperties(TState state, string environment);

        /// <summary>
        /// Gets processing data saved to the job <paramref name="id"/> with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the background job that the data should be attached to</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if Exists is set to false</returns>
        Task<(bool Exists, T Data)> TryGetDataAsync<T>(IStorageConnection connection, string id, string name, CancellationToken token = default);
        /// <summary>
        /// Creates or updates processing data with name <paramref name="name"/> to job <paramref name="id"/>.
        /// </summary>
        /// <typeparam name="T">The type of the data to save</typeparam>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the background job that the data should be saved to</param>
        /// <param name="name">The name of the data to save</param>
        /// <param name="value">The value to save</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task SetDataAsync<T>(IStorageConnection connection, string id, string name, T value, CancellationToken token = default);
    }
}
