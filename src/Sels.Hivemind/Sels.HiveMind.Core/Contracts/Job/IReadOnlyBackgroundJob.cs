using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Job.Actions;
using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a read-only background job with it's current state.
    /// </summary>
    public interface IReadOnlyBackgroundJob : IAsyncDisposable
    {
        /// <summary>
        /// The unique id of the background job.
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// The current environment of the job.
        /// </summary>
        public string Environment { get; }
        /// <summary>
        /// Unique id regenerated each time a job is persisted with a new state.
        /// Used to correlate jobs placed in a queue and the state of the job to deal with rogue messages.
        /// </summary>
        public Guid ExecutionId { get; }
        /// <summary>
        /// The name of the queue the job is placed in.
        /// </summary>
        public string Queue { get; }
        /// <summary>
        /// The priority of the job in <see cref="Queue"/>.
        /// </summary>
        public QueuePriority Priority { get; }
        /// <summary>
        /// The date (in utc) the job was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; }
        /// <summary>
        /// The date (local machine) the job was created.
        /// </summary>
        public DateTime CreatedAt => CreatedAtUtc.ToLocalTime();
        /// <summary>
        /// The last date (in utc) the job was modified.
        /// </summary>
        public DateTime ModifiedAtUtc { get; }
        /// <summary>
        /// The last date (local machine) the job was modified.
        /// </summary>
        public DateTime ModifiedAt => ModifiedAtUtc.ToLocalTime();
        /// <summary>
        /// Indicates if the current instance was deleted.
        /// </summary>
        public bool IsDeleted { get; }
        /// <summary>
        /// True if the current instance has the active lock on the job and thus can modify it, otherwise false.
        /// </summary>
        public bool HasLock { get; }

        /// <summary>
        /// Tracks the changes made on a background job.
        /// </summary>
        public IBackgroundJobChangesTracker ChangeTracker { get; }

        // State
        /// <summary>
        /// The current state of the job.
        /// </summary>
        public IBackgroundJobState State { get; }
        /// <summary>
        /// Enumerator that returns the history of the state changes of the job based on occurance. States that happened earlier will be returned first excluding <see cref="State"/>.
        /// </summary>
        public IEnumerable<IBackgroundJobState> StateHistory { get; }

        // Lock
        /// <summary>
        /// Information about the lock if the job is currently locked.
        /// </summary>
        public ILockInfo Lock { get; }
        /// <summary>
        /// True if the background job is currently locked, otherwise false.
        /// </summary>
        public bool IsLocked => Lock != null;

        // Properties
        /// <summary>
        /// The properties assigned to the background job.
        /// </summary>
        IReadOnlyDictionary<string, object> Properties { get; }
        /// <summary>
        /// Gets property with name <paramref name="name"/> casted to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="name">The name of the property to get</param>
        /// <returns>The value of property <paramref name="name"/> casted to <typeparamref name="T"/></returns>
        T GetProperty<T>(string name);
        /// <summary>
        /// Gets property with name <paramref name="name"/> casted to <typeparamref name="T"/> if the property is set.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="name">The name of the property to get</param>
        /// <returns>The value of property <paramref name="name"/> casted to <typeparamref name="T"/> or the default value of <typeparamref name="T"/> if no property exists with name <paramref name="name"/></returns>
        T GetPropertyOrDefault<T>(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if(TryGetProperty<T>(name, out var value))
            {
                return value;
            }
            return default;
        }
        /// <summary>
        /// Tries to get property with name <paramref name="name"/> casted to <typeparamref name="T"/> if the property is set.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="name">The name of the property to get</param>
        /// <param name="value">The value of the property if it exists</param>
        /// <returns>True if a property with name <paramref name="name"/> exists, otherwise false</returns>
        bool TryGetProperty<T>(string name, out T value);

        #region Data
        /// <summary>
        /// Acquires a distributed lock that can be used to synchronize data changes.
        /// Lock stays valid as long as <paramref name="connection"/> stays open.
        /// </summary>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An <see cref="IAsyncDisposable"/> that is used to define the locking scope. Disposing releases the lock</returns>
        Task<IAsyncDisposable> AcquireStateLock(IClientConnection connection, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            return AcquireStateLock(connection.StorageConnection, token);
        }
        /// <summary>
        /// Acquires a distributed lock that can be used to synchronize data changes.
        /// Lock stays valid as long as <paramref name="connection"/> stays open.
        /// </summary>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An <see cref="IAsyncDisposable"/> that is used to define the locking scope. Disposing releases the lock</returns>
        Task<IAsyncDisposable> AcquireStateLock(IStorageConnection connection, CancellationToken token = default);

        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        async Task<T> GetDataAsync<T>(IClientConnection connection, string name, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if (await TryGetDataAsync<T>(connection, name, token).ConfigureAwait(false) is (true, var data))
            {
                return data;
            }
            throw new InvalidOperationException($"Data with name <{name}> does not exists on background job <{Id}> in environment <{Environment}>");
        }
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if Exists is set to false</returns>
        Task<(bool Exists, T Data)> TryGetDataAsync<T>(IClientConnection connection, string name, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            return TryGetDataAsync<T>(connection.StorageConnection, name, token);
        }
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if no data exists with name <paramref name="name"/></returns>
        async Task<T> GetDataOrDefaultAsync<T>(IClientConnection connection, string name, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if(await TryGetDataAsync<T>(connection, name, token).ConfigureAwait(false) is (true, var data))
            {
                return data;
            }
            return default;
        }
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        async Task<T> GetDataAsync<T>(IStorageConnection connection, string name, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if (await TryGetDataAsync<T>(connection, name, token).ConfigureAwait(false) is (true, var data))
            {
                return data;
            }
            throw new InvalidOperationException($"Data with name <{name}> does not exists on background job <{Id}> in environment <{Environment}>");
        }
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if Exists is set to false</returns>
        Task<(bool Exists, T Data)> TryGetDataAsync<T>(IStorageConnection connection, string name, CancellationToken token = default);
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if no data exists with name <paramref name="name"/></returns>
        async Task<T> GetDataOrDefaultAsync<T>(IStorageConnection connection, string name, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if (await TryGetDataAsync<T>(connection, name, token).ConfigureAwait(false) is (true, var data))
            {
                return data;
            }
            return default;
        }
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        async Task<T> GetDataAsync<T>(string name, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if (await TryGetDataAsync<T>(name, token).ConfigureAwait(false) is (true, var data))
            {
                return data;
            }
            throw new InvalidOperationException($"Data with name <{name}> does not exists on background job <{Id}> in environment <{Environment}>");
        }
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if Exists is set to false</returns>
        Task<(bool Exists, T Data)> TryGetDataAsync<T>(string name, CancellationToken token = default);
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if no data exists with name <paramref name="name"/></returns>
        async Task<T> GetDataOrDefaultAsync<T>(string name, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            if (await TryGetDataAsync<T>(name, token).ConfigureAwait(false) is (true, var data))
            {
                return data;
            }
            return default;
        }

        /// <summary>
        /// Persists processing data to the current job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to save</typeparam>
        /// <param name="connection">The connection/transaction to use to save the data</param>
        /// <param name="name">The name of the data to save</param>
        /// <param name="value">The value to save</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task SetDataAsync<T>(IClientConnection connection, string name, T value, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            connection.ValidateArgument(nameof(name));

            return SetDataAsync(connection, name, value, token);
        }
        /// <summary>
        /// Persists processing data to the current job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to save</typeparam>
        /// <param name="connection">The connection/transaction to use to save the data</param>
        /// <param name="name">The name of the data to save</param>
        /// <param name="value">The value to save</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task SetDataAsync<T>(IStorageConnection connection, string name, T value, CancellationToken token = default);
        /// <summary>
        /// Persists processing data to the current job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to save</typeparam>
        /// <param name="name">The name of the data to save</param>
        /// <param name="value">The value to save</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task SetDataAsync<T>(string name, T value, CancellationToken token = default);
        #endregion

        #region Actions
        /// <summary>
        /// Schedles an action that is to be executed on a running job.
        /// </summary>
        /// <param name="connection">The connection/transaction to shedule the action with</param>
        /// <param name="actionType"><inheritdoc cref="ActionInfo.Type"/></param>
        /// <param name="actionContext"><inheritdoc cref="ActionInfo.Context"/></param>
        /// <param name="forceExecute"><inheritdoc cref="ActionInfo.ForceExecute"/></param>
        /// <param name="priority"><inheritdoc cref="ActionInfo.Priority"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when either the action is cheduled or when something goes wrong</returns>
        Task ScheduleAction(IClientConnection connection, Type actionType, object actionContext, bool forceExecute = false, byte priority = byte.MaxValue, CancellationToken token = default)
            => ScheduleAction(connection.ValidateArgument(nameof(connection)).StorageConnection, actionType, actionContext, forceExecute, priority, token);
        /// <summary>
        /// Schedles an action that is to be executed on a running job.
        /// </summary>
        /// <param name="connection">The connection/transaction to shedule the action with</param>
        /// <param name="actionType"><inheritdoc cref="ActionInfo.Type"/></param>
        /// <param name="actionContext"><inheritdoc cref="ActionInfo.Context"/></param>
        /// <param name="forceExecute"><inheritdoc cref="ActionInfo.ForceExecute"/></param>
        /// <param name="priority"><inheritdoc cref="ActionInfo.Priority"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when either the action is cheduled or when something goes wrong</returns>
        Task ScheduleAction(IStorageConnection connection, Type actionType, object actionContext, bool forceExecute = false, byte priority = byte.MaxValue, CancellationToken token = default);
        /// <summary>
        /// Schedles an action that is to be executed on a running job.
        /// </summary>
        /// <param name="connection">The connection/transaction to shedule the action with</param>
        /// <param name="actionType"><inheritdoc cref="ActionInfo.Type"/></param>
        /// <param name="actionContext"><inheritdoc cref="ActionInfo.Context"/></param>
        /// <param name="forceExecute"><inheritdoc cref="ActionInfo.ForceExecute"/></param>
        /// <param name="priority"><inheritdoc cref="ActionInfo.Priority"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when either the action is cheduled or when something goes wrong</returns>
        Task ScheduleAction(Type actionType, object actionContext, bool forceExecute = false, byte priority = byte.MaxValue, CancellationToken token = default);
        /// <summary>
        /// Schedles an action that is to be executed on a running job.
        /// </summary>
        /// <typeparam name="T"><inheritdoc cref="ActionInfo.Type"/></typeparam>
        /// <param name="connection">The connection/transaction to shedule the action with</param>
        /// <param name="actionContext"><inheritdoc cref="ActionInfo.Context"/></param>
        /// <param name="forceExecute"><inheritdoc cref="ActionInfo.ForceExecute"/></param>
        /// <param name="priority"><inheritdoc cref="ActionInfo.Priority"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when either the action is cheduled or when something goes wrong</returns>
        Task ScheduleAction<T>(IClientConnection connection, object actionContext, bool forceExecute = false, byte priority = byte.MaxValue, CancellationToken token = default) where T : IBackgroundJobAction
            => ScheduleAction(connection.ValidateArgument(nameof(connection)).StorageConnection, typeof(T), actionContext, forceExecute, priority, token);
        /// <summary>
        /// Schedles an action that is to be executed on a running job.
        /// </summary>
        /// <param name="connection">The connection/transaction to shedule the action with</param>
        /// <param name="actionContext"><inheritdoc cref="ActionInfo.Context"/></param>
        /// <param name="forceExecute"><inheritdoc cref="ActionInfo.ForceExecute"/></param>
        /// <param name="priority"><inheritdoc cref="ActionInfo.Priority"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when either the action is cheduled or when something goes wrong</returns>
        Task ScheduleAction<T>(IStorageConnection connection, object actionContext, bool forceExecute = false, byte priority = byte.MaxValue, CancellationToken token = default) where T : IBackgroundJobAction
            => ScheduleAction(connection, typeof(T), actionContext, forceExecute, priority, token);
        /// <summary>
        /// Schedles an action that is to be executed on a running job.
        /// </summary>
        /// <param name="actionContext"><inheritdoc cref="ActionInfo.Context"/></param>
        /// <param name="forceExecute"><inheritdoc cref="ActionInfo.ForceExecute"/></param>
        /// <param name="priority"><inheritdoc cref="ActionInfo.Priority"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task that will complete when either the action is cheduled or when something goes wrong</returns>
        Task ScheduleAction<T>(object actionContext, bool forceExecute = false, byte priority = byte.MaxValue, CancellationToken token = default) where T : IBackgroundJobAction
            => ScheduleAction(typeof(T), actionContext, forceExecute, priority, token);
        #endregion

        #region TryLock
        /// <summary>
        /// Try to get an exclusive lock on the current background job for <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: true if the lock was acquired, otherwise false.  LockedBackgroundJob: The current background job with a lock if it could be acquired, otherwise null</returns>
        Task<(bool WasLocked, ILockedBackgroundJob LockedBackgroundJob)> TryLockAsync(IClientConnection connection, string requester = null, CancellationToken token = default)
        => TryLockAsync(connection.ValidateArgument(nameof(connection)).StorageConnection, requester, token);
        /// <summary>
        /// Try to get an exclusive lock on the current background job for <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: true if the lock was acquired, otherwise false.  LockedBackgroundJob: The current background job with a lock if it could be acquired, otherwise null</returns>
        Task<(bool WasLocked, ILockedBackgroundJob LockedBackgroundJob)> TryLockAsync(IStorageConnection connection, string requester = null, CancellationToken token = default);
        /// <summary>
        /// Try to get an exclusive lock on the current background job for <paramref name="requester"/>.
        /// </summary>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: true if the lock was acquired, otherwise false.  LockedBackgroundJob: The current background job with a lock if it could be acquired, otherwise null</returns>
        Task<(bool WasLocked, ILockedBackgroundJob LockedBackgroundJob)> TryLockAsync(string requester = null, CancellationToken token = default);
        #endregion

        // Invocation
        /// <summary>
        /// Contains the invocation data on how to execute the job.
        /// </summary>
        public IInvocationInfo Invocation { get; }

        // Middleware
        /// <summary>
        /// Any middleware defined for the background job.
        /// </summary>
        public IReadOnlyList<IMiddlewareInfo> Middleware { get; }

        /// <summary>
        /// Try to get an exclusive lock on the current background job for <paramref name="requester"/>.
        /// </summary>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The current background job with a lock if it could be acquired</returns>
        public Task<ILockedBackgroundJob> LockAsync(string requester = null, CancellationToken token = default);
        /// <summary>
        /// Try to get an exclusive lock on the current background job for <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The current background job with a lock if it could be acquired</returns>
        public Task<ILockedBackgroundJob> LockAsync(IStorageConnection connection, string requester = null, CancellationToken token = default);
        /// <summary>
        /// Try to get an exclusive lock on the current background job for <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The current background job with a lock if it could be acquired</returns>
        public Task<ILockedBackgroundJob> LockAsync(IClientConnection connection, string requester = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            return LockAsync(connection.StorageConnection, requester, token);
        }

        /// <summary>
        /// Refreshes the state of the current job to get the latest changes.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns>Task containing the execution state</returns>
        public Task RefreshAsync(CancellationToken token = default);
        /// <summary>
        /// Refreshes the state of the current job to get the latest changes.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns>Task containing the execution state</returns>
        public Task RefreshAsync(IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Refreshes the state of the current job to get the latest changes.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns>Task containing the execution state</returns>
        public Task RefreshAsync(IClientConnection connection, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            return RefreshAsync(connection.StorageConnection, token);
        }

        /// <summary>
        /// Tries to cancel the background job if it is in state <see cref="EnqueuedState"/> or <see cref="ExecutingState"/>.
        /// </summary>
        /// <param name="connection">The connection to cancel the job with</param>
        /// <param name="requester">Who is requesting the cancellation</param>
        /// <param name="reason">Why cancellation is requested</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was cancelled right away, false when a <see cref="CancelBackgroundJobAction"/> was scheduled to cancel the running job or null if the job wasn't in the correct state</returns>
        public Task<bool?> CancelAsync(IClientConnection connection, string requester = null, string reason = null, CancellationToken token = default)
            => CancelAsync(connection.ValidateArgument(nameof(connection)), requester, reason, token);
        /// <summary>
        /// Tries to cancel the background job if it is in state <see cref="EnqueuedState"/> or <see cref="ExecutingState"/>.
        /// </summary>
        /// <param name="connection">The connection to cancel the job with</param>
        /// <param name="requester">Who is requesting the cancellation</param>
        /// <param name="reason">Why cancellation is requested</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was cancelled right away, false when a <see cref="CancelBackgroundJobAction"/> was scheduled to cancel the running job or null if the job wasn't in the correct state</returns>
        public Task<bool?> CancelAsync(IStorageConnection connection, string requester = null, string reason = null, CancellationToken token = default);
        /// <summary>
        /// Tries to cancel the background job if it is in state <see cref="EnqueuedState"/> or <see cref="ExecutingState"/>.
        /// </summary>
        /// <param name="requester">Who is requesting the cancellation</param>
        /// <param name="reason">Why cancellation is requested</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the job was cancelled right away, false when a <see cref="CancelBackgroundJobAction"/> was scheduled to cancel the running job or null if the job wasn't in the correct state</returns>
        public Task<bool?> CancelAsync(string requester = null, string reason = null, CancellationToken token = default);
    }
}
