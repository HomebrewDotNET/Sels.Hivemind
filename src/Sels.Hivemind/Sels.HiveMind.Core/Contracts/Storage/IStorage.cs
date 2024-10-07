using Microsoft.Extensions.Logging;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job.Background;
using Sels.HiveMind.Storage.Job.Recurring;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Storage.Colony;
using Sels.HiveMind.Query.Colony;

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
        Task<string> CreateBackgroundJobAsync(BackgroundJobStorageData jobData, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of background job <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The latest state of background job <paramref name="id"/> or null if the job doesn't exist</returns>
        Task<BackgroundJobStorageData> GetBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default);
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
        Task<BackgroundJobStorageData[]> SearchBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries background jobs and counts how many jobs match the uqery condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many jobs match the query condition</returns>
        Task<long> CountBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default);
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
        Task<BackgroundJobStorageData[]> LockBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of background job <paramref name="id"/> if it exists optionally with a lock for <paramref name="requester"/>.
        /// </summary>
        /// <param name="id">The id of the background job to lock</param>
        /// <param name="requester">Who is requesting the lock. Should only acquire the lock if the job is not locked or already locked by <paramref name="requester"/></param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: true if the job was locked | Data: The latest state of the job if it exists</returns>
        Task<(bool WasLocked, BackgroundJobStorageData Data)> TryLockAndTryGetBackgroundJobAsync(string id, string requester, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Tries to keep the lock on background job <paramref name="id"/> by <paramref name="holder"/> alive by extending the heartbeat.
        /// </summary>
        /// <param name="id">The id of the job to set the heartbeat on</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasExtended: True if the lock was extended for <paramref name="holder"/>|Data: The current lock state on the job regardless of WasExtended</returns>
        Task<(bool WasExtended, LockStorageData Data)> TryHeartbeatLockOnBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
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
        Task<bool> TryUpdateBackgroundJobAsync(BackgroundJobStorageData jobData, IStorageConnection connection, bool releaseLock, CancellationToken token = default);
        /// <summary>
        /// Removes job <paramref name="id"/> if it is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="id">The id of the job to delete</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was deleted, otherwise false</returns>
        Task<bool> TryDeleteBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Tries to remove jobs <paramref name="ids"/> if it is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="ids">The ids of the jobs to delete</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The ids of the jobs that were deleted</returns>
        Task<string[]> TryDeleteBackgroundJobsAsync(string[] ids, string holder, IStorageConnection connection, CancellationToken token = default);
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
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the background job to the data should be attached to</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The serialized data or null if Exists is set to false</returns>
        Task<(bool Exists, string Data)> TryGetBackgroundJobDataAsync(IStorageConnection connection, string id, string name, CancellationToken token = default);
        /// <summary>
        /// Creates or updates data with name <paramref name="name"/> to background job <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the background job to save the data to</param>
        /// <param name="name">The name of the data to save</param>
        /// <param name="value">The serialized data to store</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task SetBackgroundJobDataAsync(IStorageConnection connection, string id, string name, string value, CancellationToken token = default);
        /// <summary>
        /// Fetches locked background jobs where the last heartbeat on the lock was longer than <paramref name="timeoutThreshold"/> ago.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="limit">The maximum amount of jobs to return</param>
        /// <param name="requester">Who is requesting the locked jobs</param>
        /// <param name="timeoutThreshold">How long after the last heartbeat on a lock before the lock is considered timed out</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with the storage data of all timed out background jobs</returns>
        Task<BackgroundJobStorageData[]> GetTimedOutBackgroundJobs(IStorageConnection connection, int limit, string requester, TimeSpan timeoutThreshold, CancellationToken token = default);
        /// <summary>
        /// Returns all distinct queues being used by all background jobs optionally that start with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="prefix">Optional filter on the queues to only include the queues that start with the prefix</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct background job queues matching <paramref name="prefix"/> if defined or an empty array when there are no background jobs</returns>
        Task<string[]> GetAllBackgroundJobQueuesAsync(IStorageConnection connection, string? prefix = null, CancellationToken token = default);
        /// <summary>
        /// Creates <paramref name="action"/> in the storage and assigns a unique id to it.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="action">The action to create</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when <paramref name="action"/> is created</returns>
        Task CreateBackgroundJobActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default);
        /// <summary>
        /// Fetches the next <paramref name="limit"/> actions defined for background job <paramref name="id"/> ordered by priority.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the background job to fetch the actions for</param>
        /// <param name="limit">The maximum amount of actions to return</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with actions defined for background job <paramref name="id"/> or an empty array when nothing is defined</returns>
        Task<ActionInfo[]> GetNextBackgroundJobActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default);
        /// <summary>
        /// Attempts to delete background job action <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the action to delete</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if action <paramref name="id"/> was deleted, otherwise false</returns>
        Task<bool> DeleteBackgroundJobActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default);
        #endregion

        #region Recurring job
        /// <summary>
        /// Tries to create recurring job using the configuration in <paramref name="storageData"/> if it does not exist yet.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="storageData">The state used to create the new recurring job</param>
        /// <param name="token">Optional token that can be used to cancel the request</param>
        /// <returns>The current state of the recurring job</returns>
        Task<RecurringJobStorageData> TryCreateAsync(IStorageConnection connection, RecurringJobConfigurationStorageData storageData, CancellationToken token = default);
        /// <summary>
        /// Updates a job in the storage. Should check lock ownership.
        /// </summary>
        /// <param name="jobData">The data of the job to update</param>
        /// <param name="releaseLock">If the lock has to be removed</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the update was successful, otherwise false</returns>
        Task<bool> TryUpdateRecurringJobAsync(IStorageConnection connection, RecurringJobStorageData jobData, bool releaseLock, CancellationToken token = default);
        /// <summary>
        /// Removes job <paramref name="id"/> if it is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="id">The id of the job to delete</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was deleted, otherwise false</returns>
        Task<bool> TryDeleteRecurringJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of recurring job <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the recurring job to fetch</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The latest state of recurring job <paramref name="id"/> or null if the job doesn't exist</returns>
        Task<RecurringJobStorageData> GetRecurringJobAsync(string id, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Creates <paramref name="action"/> in the storage and assigns a unique id to it.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="action">The action to create</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when <paramref name="action"/> is created</returns>
        Task CreateRecurringJobActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default);
        /// <summary>
        /// Fetches the next <paramref name="limit"/> actions defined for recurring job <paramref name="id"/> ordered by priority.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the recurring job to fetch the actions for</param>
        /// <param name="limit">The maximum amount of actions to return</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with actions defined for recurring job <paramref name="id"/> or an empty array when nothing is defined</returns>
        Task<ActionInfo[]> GetNextRecurringJobActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default);
        /// <summary>
        /// Attempts to delete recurring job action <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the action to delete</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if action <paramref name="id"/> was deleted, otherwise false</returns>
        Task<bool> DeleteRecurringJobActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default);
        /// <summary>
        /// Persists all logs in <paramref name="logEntries"/> that are tied to recurring job <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the recurring job to persist the log entries for</param>
        /// <param name="logEntries">Enumerator that returns the log entries to persist</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task CreateRecurringJobLogsAsync(IStorageConnection connection, string id, IEnumerable<LogEntry> logEntries, CancellationToken token = default);
        /// <summary>
        /// Fetches the logs for recurring job <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the recurring job to fetch the logs for</param>
        /// <param name="logLevels">Optional filter on the log level of the entries. When null or empty all logs will be returned</param>
        /// <param name="page">The page of the logs to return</param>
        /// <param name="pageSize">How many log entries to return per page</param>
        /// <param name="mostRecentFirst">True to order by created descending, false to order by ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>All log entries for recurring job <paramref name="id"/> matching the filters or an empty array when there are no logs to return</returns>
        Task<LogEntry[]> GetRecurringJobLogsAsync(IStorageConnection connection, string id, LogLevel[] logLevels, int page, int pageSize, bool mostRecentFirst, CancellationToken token = default);
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the recurring job to the data should be attached to</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The serialized data or null if Exists is set to false</returns>
        Task<(bool Exists, string Data)> TryGetRecurringJobDataAsync(IStorageConnection connection, string id, string name, CancellationToken token = default);
        /// <summary>
        /// Creates or updates data with name <paramref name="name"/> to recurring job <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">The id of the recurring job to save the data to</param>
        /// <param name="name">The name of the data to save</param>
        /// <param name="value">The serialized data to store</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task SetRecurringJobDataAsync(IStorageConnection connection, string id, string name, string value, CancellationToken token = default);
        /// <summary>
        /// Fetches locked recurring jobs where the last heartbeat on the lock was longer than <paramref name="timeoutThreshold"/> ago.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="limit">The maximum amount of jobs to return</param>
        /// <param name="requester">Who is requesting the locked jobs</param>
        /// <param name="timeoutThreshold">How long after the last heartbeat on a lock before the lock is considered timed out</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with the storage data of all timed out recurring jobs</returns>
        Task<RecurringJobStorageData[]> GetTimedOutRecurringJobs(IStorageConnection connection, int limit, string requester, TimeSpan timeoutThreshold, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of recurring job <paramref name="id"/> if it exists optionally with a lock for <paramref name="requester"/>.
        /// </summary>
        /// <param name="id">The id of the recurring job to lock</param>
        /// <param name="requester">Who is requesting the lock. Should only acquire the lock if the job is not locked or already locked by <paramref name="requester"/></param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: true if the job was locked | Data: The latest state of the job if it exists</returns>
        Task<(bool WasLocked, RecurringJobStorageData Data)> TryLockAndTryGetRecurringJobAsync(string id, string requester, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Tries to keep the lock on recurring job <paramref name="id"/> by <paramref name="holder"/> alive by extending the heartbeat.
        /// </summary>
        /// <param name="id">The id of the job to set the heartbeat on</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasExtended: True if the lock was extended for <paramref name="holder"/>|Data: The current lock state on the job regardless of WasExtended</returns>
        Task<(bool WasExtended, LockStorageData Data)> TryHeartbeatLockOnRecurringJobAsync(IStorageConnection connection, string id, string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to release the lock on recurring job <paramref name="id"/> if it is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="id">The id of the job to unlock</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the lock was released, otherwise false</returns>
        Task<bool> UnlockRecurringJobAsync(IStorageConnection connection, string id, string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to release the locks on recurring jobs <paramref name="ids"/> if they are still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="id">The ids of the jobs to unlock</param>
        /// <param name="holder">Who is supposed to hold the locks</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        Task UnlockRecurringJobsAsync(string[] ids, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Queries recurring jobs.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The storage data of all jobs matching the query conditions and the total amount of jobs that match the query condition</returns>
        Task<RecurringJobStorageData[]> SearchRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries recurring jobs and counts how many jobs match the uqery condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many jobs match the query condition</returns>
        Task<long> CountRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default);
        /// <summary>
        /// Attempts to lock the first <paramref name="limit"/> recurring jobs that match the query condition.
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
        Task<RecurringJobStorageData[]> LockRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Returns all distinct queues being used by all recurring jobs optionally that start with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="prefix">Optional filter on the queues to only include the queues that start with the prefix</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct recurring job queues matching <paramref name="prefix"/> if defined or an empty array when there are no recurring jobs</returns>
        Task<string[]> GetAllRecurringJobQueuesAsync(IStorageConnection connection, string? prefix = null, CancellationToken token = default);
        /// <summary>
        /// Removes older data linked to recurring job <paramref name="id"/> based on the supplied modes
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="id">Id of the recurring job to apply the retention to</param>
        /// <param name="stateRetentionMode"><inheritdoc cref="IRecurringJobSettings.StateRetentionMode"/></param>
        /// <param name="stateRetentionAmount"><inheritdoc cref="IRecurringJobSettings.StateRetentionAmount"/></param>
        /// <param name="logRetentionMode"><inheritdoc cref="IRecurringJobSettings.LogRetentionMode"/></param>
        /// <param name="logRetentionAmount"><inheritdoc cref="IRecurringJobSettings.StateRetentionAmount"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Tuple with the amount of states and logs removed if any where removed.</returns>
        Task<(int StatesRemoved, int LogsRemoved)> ApplyRetention(IStorageConnection connection, string id, RecurringJobRetentionMode stateRetentionMode, int stateRetentionAmount, RecurringJobRetentionMode logRetentionMode, int logRetentionAmount, CancellationToken token = default);
        #endregion

        #region Colony
        /// <summary>
        /// Tries to sync the current state of <paramref name="colony"/> to the storage if it can get the process lock on the colony.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="colony">The colony transformed into a format for storage.</param>
        /// <param name="requester">Who is requesting the process lock</param>
        /// <param name="timeout">How long after the lock heartbeat before a timed out colony can be acquired again by another process</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: If the lock on <paramref name="colony"/> was acquired|Lock: The current state of the lock on <paramref name="colony"/> regardless if the lock was acquired</returns>
        public Task<(bool WasLocked, LockStorageData Lock)> TrySyncAndGetProcessLockOnColonyAsync(IStorageConnection connection, ColonyStorageData colony, string requester, TimeSpan timeout, CancellationToken token = default);
        /// <summary>
        /// Tries to heartbeat the process lock on colony with <paramref name="colonyId"/> for <paramref name="holder"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="colonyId">The id of the colony the heartbeat the lock on</param>
        /// <param name="holder">Who is supposed to have the process lock on colony with <paramref name="colonyId"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>HeartbeatWasSet: If the heartbeat on colony with <paramref name="colonyId"/> was set for <paramref name="holder"/>|Lock: The current state of the lock on colony with <paramref name="colonyId"/> regardless if the heartbeat was set. Can be null if the colony was deleted</returns>
        public Task<(bool HeartbeatWasSet, LockStorageData Lock)> TryHeartbeatProcessLockOnColonyAsync(IStorageConnection connection, [Traceable(HiveLog.Colony.Id)] string colonyId, [Traceable(HiveLog.Colony.Holder)] string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to sync the current state of <paramref name="colony"/> to the storage if the process lock is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="colony">The colony transformed into a format for storage. <see cref="ColonyStorageData.LockStorageData"/> should contain the one requesting the lock</param>
        /// <param name="holder">Who is supposed to have the process lock on colony with <paramref name="colony"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the state was persisted or false if <paramref name="holder"/> doesn't hold the lock anymore</returns>
        public Task<bool> TrySyncColonyAsync(IStorageConnection connection, ColonyStorageData colony, [Traceable(HiveLog.Colony.Holder)] string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to release the process lock on colony with <paramref name="colonyId"/> if it is still held by <paramref name="holder"/>.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="colonyId">The id of the colony to release the lock from</param>
        /// <param name="holder">Who is supposed to have the process lock on colony with <paramref name="colonyId"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the lock was released for <paramref name="holder"/>, otherwise false</returns>
        public Task<bool> ReleaseLockOnColonyIfHeldByAsync(IStorageConnection connection, [Traceable(HiveLog.Colony.Id)] string colonyId, [Traceable(HiveLog.Colony.Holder)] string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to fetch the full state of the colony with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state if it exists, otherwise null</returns>
        public Task<ColonyStorageData> TryGetColonyAsync(IStorageConnection connection, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default);
        /// <summary>
        /// Queries colonies.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which jobs to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The storage data of all colonies matching the query conditions</returns>
        Task<ColonyStorageData[]> SearchColoniesAsync(IStorageConnection connection, ColonyQueryConditions queryConditions, int pageSize, int page, QueryColonyOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries colonies and counts how many colonies match the query condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="queryConditions">The conditions for which colonies to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many colonies match the query condition</returns>
        Task<long> CountColoniesAsync(IStorageConnection connection, ColonyQueryConditions queryConditions, CancellationToken token = default);

        #endregion
    }
}
