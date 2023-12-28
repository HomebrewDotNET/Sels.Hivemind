using Microsoft.Extensions.Logging;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Job;
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

        #region Background job
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
        Task<(JobStorageData[] Results, long Total)> SearchBackgroundJobsAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries background jobs and counts how many jobs match the uqery condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many jobs match the query condition</returns>
        Task<long> CountBackgroundJobsAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, CancellationToken token = default);
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
        Task<(JobStorageData[] Results, long Total)> LockBackgroundJobsAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
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
        /// Tries to release the locks on background jobs <paramref name="ids"/> if they are still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="id">The ids of the jobs to unlock</param>
        /// <param name="holder">Who is supposed to hold the locks</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        Task UnlockBackgroundsJobAsync(string[] ids, string holder, IStorageConnection connection, CancellationToken token = default);
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
        /// <summary>
        /// Persists all logs in <paramref name="logEntries"/> that are tied to background job <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the background job to persist the log entries for</param>
        /// <param name="logEntries">Enumerator that returns the log entries to persist</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task CreateBackgroundJobLogsAsync(IStorageConnection connection, string id, IEnumerable<LogEntry> logEntries, CancellationToken token = default);
        /// <summary>
        /// Fetches the logs for background job <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the background job to fetch the logs for</param>
        /// <param name="logLevels">Optional filter on the log level of the entries. When null or empty all logs will be returned</param>
        /// <param name="page">The page of the logs to return</param>
        /// <param name="pageSize">How many log entries to return per page</param>
        /// <param name="mostRecentFirst">True to order by created descending, false to order by ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>All log entries for background job <paramref name="id"/> matching the filters or an empty array when there are no logs to return</returns>
        Task<LogEntry[]> GetBackgroundJobLogsAsync(IStorageConnection connection, string id, LogLevel[] logLevels, int page, int pageSize, bool mostRecentFirst, CancellationToken token = default);
        #endregion

    }
}
