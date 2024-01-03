using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Sels.HiveMind.Client;
using Sels.HiveMind.Query.Job;
using Sels.Core.Extensions;
using System.Linq;

namespace Sels.HiveMind.Service.Job
{
    /// <summary>
    /// Service used for managing HiveMind background jobs.
    /// </summary>
    public interface IBackgroundJobService
    {
        /// <summary>
        /// Creates or updates <paramref name="job"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="job">The job to save. If <see cref="JobStorageData.Id"/> is set to null job will be created, otherwise updated</param>
        /// <param name="releaseLock">If the lock on the job has to be removed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job or <see cref="JobStorageData.Id"/> if the job was updated</returns>
        public Task<string> StoreAsync(IStorageConnection connection, JobStorageData job, bool releaseLock, CancellationToken token = default);

        /// <summary>
        /// Tries to acquire an exclusive lock on background job <paramref name="id"/> for <paramref name="requester"/>.
        /// Will throw if lock could nopt be acquired.
        /// </summary>
        /// <param name="id">The id of the job to lock</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The lock state if a lock could be acquired</returns>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        /// <exception cref="BackgroundJobAlreadyLockedException"></exception>
        public Task<LockStorageData> LockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default);
        /// <summary>
        /// Tries to acquire an exclusive lock on background job <paramref name="id"/> for <paramref name="requester"/>.
        /// </summary>
        /// <param name="id">The id of the job to lock</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if a lock was acquired by <paramref name="requester"/>, otherwise false</returns>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        public Task<bool> TryLockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default);
        /// <summary>
        /// Keep the lock on background job <paramref name="id"/> by <paramref name="holder"/> alive by extending the heartbeat.
        /// </summary>
        /// <param name="id">The id of the job to set the heartbeat on</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The lock state if the heartbeat was extended</returns>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        /// <exception cref="BackgroundJobAlreadyLockedException"></exception>
        public Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of background job <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        /// <returns>The latest state of background job <paramref name="id"/></returns>
        public Task<JobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Queries background jobs.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The storage data of all jobs matching the query conditions and the total amount of jobs that match the query condition</returns>
        public Task<(JobStorageData[] Results, long Total)> SearchAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries background jobs and counts how many jobs match the uqery condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many jobs match the query condition</returns>
        public Task<long> CountAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, CancellationToken token = default);
        /// <summary>
        /// Attempts to lock the first <paramref name="limit"/> background jobs that match the query condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to return</param>
        /// <param name="limit">The maximum amount of jobs to lock</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used</param>
        /// <param name="allowAlreadyLocked">If jobs already locked by <paramref name="requester"/> can be returned as well, otherwise false to return only jobs that weren't locked</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The storage data of all jobs matching the query conditions that could be locked and the total amount of jobs that match the query condition</returns>
        public Task<(JobStorageData[] Results, long Total)> LockAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Fetches locked background jobs where the last heartbeat on the lock was longer than the configured timeout for the HiveMind environment.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="limit">The maximum amount of jobs to return</param>
        /// <param name="requester">Who is requesting the locked jobs</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with the storage data of all timed out background jobs</returns>
        Task<JobStorageData[]> GetTimedOutBackgroundJobs(IStorageConnection connection, int limit, string requester, CancellationToken token = default);

        /// <summary>
        /// Converts state in storage format back into it's original type.
        /// </summary>
        /// <param name="stateData">The state data</param>
        /// <param name="options">The options to convert with</param>
        /// <returns>State instance created from <paramref name="stateData"/></returns>
        public IBackgroundJobState ConvertToState(JobStateStorageData stateData, HiveMindOptions options);
        /// <summary>
        /// Gets all the properties to store for <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to get the properties for</param>
        /// <param name="options">The options for caching</param>
        /// <returns>All the state properties to store for <paramref name="state"/> if there are any</returns>
        public IEnumerable<StorageProperty> GetStorageProperties(IBackgroundJobState state, HiveMindOptions options);

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
