using Sels.HiveMind.Middleware.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.HiveMind.Models.Queue;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Client for creating, fetching and querying background jobs.
    /// </summary>
    public interface IBackgroundJobClient : IClient
    {
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(IClientConnection connection, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class;
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync<T>(Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            await using (var connection = await OpenConnectionAsync(HiveMindConstants.DefaultEnvironmentName, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync<T>(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(IClientConnection connection, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync(Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            await using (var connection = await OpenConnectionAsync(HiveMindConstants.DefaultEnvironmentName, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
    }

    /// <summary>
    /// Builder for configuring additional options on background job during creation.
    /// </summary>
    public interface IBackgroundJobBuilder
    {
        /// <summary>
        /// The client used to create the background job.
        /// </summary>
        IBackgroundJobClient Client { get; }
        /// <summary>
        /// The current connection the job is being created with.
        /// </summary>
        IClientConnection Connection { get; }

        /// <summary>
        /// Places the job in <paramref name="queue"/>.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <param name="priority">The priority of the job in <paramref name="priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder InQueue(string queue, QueuePriority priority = QueuePriority.Normal);
        /// <summary>
        /// Adds a property to the job.
        /// </summary>
        /// <param name="name">The name of the property to add</param>
        /// <param name="value">The value of the property</param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder WithProperty(string name, object value);
        /// <summary>
        /// Changes the state of the job to <paramref name="state"/> through state election during creation.
        /// Depending on the state election, the final state after creation might not be <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to elect</param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder InState(IBackgroundJobState state);

        /// <summary>
        /// Defines a middleware to use when executing the job.
        /// </summary>
        /// <typeparam name="T">The type of the middleware to add</typeparam>
        /// <param name="context"><inheritdoc cref="IMiddlewareInfo.Context"/></param>
        /// <param name="priority"><inheritdoc cref="IMiddlewareInfo.Priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder WithMiddleWare<T>(object context = null, uint? priority = null) where T : class, IBackgroundJobMiddleware;


        // Overloads
        /// <summary>
        /// Job will only be executed after <paramref name="date"/>.
        /// </summary>
        /// <param name="date">The date after which the job can be picked up</param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder DelayExecutionTo(DateTime date)
        {
            return InState(new EnqueuedState(date));
        }
    }
}
