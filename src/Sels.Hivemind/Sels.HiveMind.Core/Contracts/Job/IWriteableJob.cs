using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a job that can be modified.
    /// Contains common state properties that are available on all jobs.
    /// </summary>
    /// <typeparam name="TLockedJob">The type of the locked job</typeparam>
    /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
    public interface IWriteableJob<TLockedJob, TChangeTracker, TState, TAction> : IReadOnlyJob<TLockedJob, TChangeTracker, TState, TAction>
        where TState : IJobState
        where TChangeTracker : IJobChangeTracker<TState>
    {
        // Queue
        /// <summary>
        /// Changes the current queue of the job.
        /// Job must be rescheduled for it to be processed again.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <param name="priority">The priority of the job in <paramref name="queue"/></param>
        /// <returns>Current job for method chaining</returns>
        void ChangeQueue(string queue, QueuePriority priority);
        /// <summary>
        /// Changes the current queue of the job.
        /// Job must be rescheduled for it to be processed again.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <returns>Current job for method chaining</returns>
        void ChangeQueue(string queue) => ChangeQueue(queue, Priority);
        /// <summary>
        /// Changes the priority of the current job in the current queue.
        /// Job must be rescheduled for it to be processed again.
        /// </summary>
        /// <param name="priority">The new priority for the job in the current queue</param>
        /// <returns>Current job for method chaining</returns>
        void ChangePriority(QueuePriority priority) => ChangeQueue(Queue, priority);

        // State
        /// <summary>
        /// Triggers state election to try and change the state of the job to <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to transition into</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the current state was changed to <paramref name="state"/>, false if state election resulted in another state being elected</returns>
        Task<bool> ChangeStateAsync(TState state, CancellationToken token = default)
            => ChangeStateAsync((IStorageConnection)null, state, token);
        /// <summary>
        /// Triggers state election to try and change the state of the job to <paramref name="state"/>.
        /// </summary>
        /// <param name="storageConnection">Optional connection to change the state with. Gives handlers access to the same transaction</param>
        /// <param name="state">The state to transition into</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the current state was changed to <paramref name="state"/>, false if state election resulted in another state being elected</returns>
        Task<bool> ChangeStateAsync(IStorageConnection storageConnection, TState state, CancellationToken token = default);
        /// <summary>
        /// Triggers state election to try and change the state of the job to <paramref name="state"/>.
        /// </summary>
        /// <param name="connection">Optional connection to change the state with. Gives handlers access to the same transaction</param>
        /// <param name="state">The state to transition into</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the current state was changed to <paramref name="state"/>, false if state election resulted in another state being elected</returns>
        Task<bool> ChangeStateAsync(IClientConnection connection, TState state, CancellationToken token = default)
            => ChangeStateAsync(connection.ValidateArgument(nameof(connection)).StorageConnection, state, token);

        // Property
        /// <summary>
        /// Adds or updates property with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the property value to set</typeparam>
        /// <param name="name">The name of the property to set</param>
        /// <param name="value">The value to set on the property</param>
        /// <returns>Current job for method chaining</returns>
        void SetProperty<T>(string name, T value);
        /// <summary>
        /// Removes property with name <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the property to remove</param>
        /// <returns>Current job for method chaining</returns>
        void RemoveProperty(string name);

        /// <summary>
        /// Fetches property with <paramref name="name"/> and casts it to <typeparamref name="T"/>.
        /// If the property does not exist it will be set using <paramref name="initializer"/> and returned.
        /// </summary>
        /// <typeparam name="T">The type of the property value to get/set</typeparam>
        /// <param name="name">The name of the property to get/set</param>
        /// <param name="initializer">Delegate used to initialize the property</param>
        /// <returns>The value of property with <paramref name="name"/> or the value returned by <paramref name="initializer"/> if the property didn't exist yet</returns>
        T GetPropertyOrSet<T>(string name, Func<T> initializer)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            initializer.ValidateArgument(nameof(initializer));

            if (!TryGetProperty<T>(name, out var value))
            {
                value = initializer();
                SetProperty<T>(name, value);
            }

            return value;
        }
    }
}
