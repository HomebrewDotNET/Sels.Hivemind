using Sels.HiveMind.Job;
using Sels.HiveMind.Models.Queue;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a background job that can be modified.
    /// </summary>
    public interface IWriteableBackgroundJob : IReadOnlyBackgroundJob
    {
        // Queue
        /// <summary>
        /// Changes the current queue of the job.
        /// Job must be rescheduled for it to be processed again.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <param name="priority">The priority of the job in <paramref name="queue"/></param>
        /// <returns>Current job for method chaining</returns>
        IWriteableBackgroundJob ChangeQueue(string queue, QueuePriority priority);
        /// <summary>
        /// Changes the current queue of the job.
        /// Job must be rescheduled for it to be processed again.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <returns>Current job for method chaining</returns>
        IWriteableBackgroundJob ChangeQueue(string queue) => ChangeQueue(queue, Priority);
        /// <summary>
        /// Changes the priority of the current job in the current queue.
        /// Job must be rescheduled for it to be processed again.
        /// </summary>
        /// <param name="priority">The new priority for the job in the current queue</param>
        /// <returns>Current job for method chaining</returns>
        IWriteableBackgroundJob ChangePriority(QueuePriority priority) => ChangeQueue(Queue, priority);

        // State
        /// <summary>
        /// Triggers state election to try and change the state of the job to <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to transition into</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the current state was changed to <paramref name="state"/>, false if state election resulted in another state being elected</returns>
        Task<bool> ChangeStateAsync(IBackgroundJobState state, CancellationToken token = default);

        // Property
        /// <summary>
        /// Adds or updates property with name <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the property value to set</typeparam>
        /// <param name="name">The name of the property to set</param>
        /// <param name="value">The value to set on the property</param>
        /// <returns>Current job for method chaining</returns>
        IWriteableBackgroundJob SetProperty<T>(string name, T value);
        /// <summary>
        /// Removes property with name <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the property to remove</param>
        /// <returns>Current job for method chaining</returns>
        IWriteableBackgroundJob RemoveProperty(string name);

        // Other
        /// <summary>
        /// Regnerates <see cref="IReadOnlyBackgroundJob.ExecutionId"/>.
        /// </summary>
        /// <returns>Current job for method chaining</returns>
        IWriteableBackgroundJob RegenerateExecutionId();
    }
}
