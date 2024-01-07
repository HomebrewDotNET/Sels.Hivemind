﻿using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
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
        /// Gets processing data saved to the job with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The client connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The data converted into an instance of <typeparamref name="T"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        async Task<T> GetDataAsync<T>(IClientConnection connection, string name, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
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
        /// <param name="connection">The client connection to use to execute the request</param>
        /// <param name="name">The name of the data to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Exists: True if data with name <paramref name="name"/> exists, otherwise false | Data: The data converted into an instance of <typeparamref name="T"/> or the default of <typeparamref name="T"/> if Exists is set to false</returns>
        Task<(bool Exists, T Data)> TryGetDataAsync<T>(IClientConnection connection, string name, CancellationToken token = default);
        /// <summary>
        /// Gets processing data saved to the job with name <paramref name="name"/> if it exists.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored data</typeparam>
        /// <param name="connection">The client connection to use to execute the request</param>
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
        public Task<ILockedBackgroundJob> LockAsync(IClientConnection connection, string requester = null, CancellationToken token = default);

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
        public Task RefreshAsync(IClientConnection connection, CancellationToken token = default);
    }
}
