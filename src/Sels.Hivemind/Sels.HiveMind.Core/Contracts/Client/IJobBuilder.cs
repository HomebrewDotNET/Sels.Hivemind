using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for configuring additional options on jobs during creation.
    /// </summary>
    /// <typeparam name="TBuilder">The type of builder to return for the fluent syntax</typeparam>
    public interface IJobBuilder<TBuilder>
    {
        /// <summary>
        /// The current connection the job is being created with.
        /// </summary>
        IStorageConnection Connection { get; }
        /// <summary>
        /// The current queue the job is being created with.
        /// </summary>
        string Queue { get; }
        /// <summary>
        /// The current priority the job is being created with.
        /// </summary>
        QueuePriority Priority { get; }
        /// <summary>
        /// The current middleware the job is being created with.
        /// </summary>
        IReadOnlyList<MiddlewareInfo> Middleware { get; }
        /// <summary>
        /// The current properties the job is being created with.
        /// </summary>
        IReadOnlyDictionary<string, object> Properties { get; }

        /// <summary>
        /// Places the job in queue <paramref name="queue"/> with a priority of <paramref name="priority"/>.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <param name="priority">The priority of the job in <paramref name="priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        TBuilder InQueue(string queue, QueuePriority priority = QueuePriority.Normal);
        /// <summary>
        /// Places the job in queue <see cref="HiveMindConstants.Queue.DefaultQueue"/> with a priority of <paramref name="priority"/>
        /// </summary>
        /// <param name="priority">The priority of the job in <paramref name="priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        TBuilder WithPriority(QueuePriority priority) => InQueue(HiveMindConstants.Queue.DefaultQueue, priority);
        /// <summary>
        /// Adds a property to the job.
        /// </summary>
        /// <param name="name">The name of the property to add</param>
        /// <param name="value">The value of the property</param>
        /// <returns>Current builder for method chaining</returns>
        TBuilder WithProperty(string name, object value);
        /// <summary>
        /// Defines a middleware to use when executing the job.
        /// </summary>
        /// <param name="middlewareType">The type of the middleware to add</param>
        /// <param name="context"><inheritdoc cref="IMiddlewareInfo.Context"/></param>
        /// <param name="priority"><inheritdoc cref="IMiddlewareInfo.Priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        TBuilder WithMiddleWare(Type middlewareType, object? context = null, byte? priority = null);
    }
}
