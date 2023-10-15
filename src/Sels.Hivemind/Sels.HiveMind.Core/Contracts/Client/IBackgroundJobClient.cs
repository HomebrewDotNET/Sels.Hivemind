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
using Sels.HiveMind.Queue;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Client for creating, fetching and querying background jobs.
    /// </summary>
    public interface IBackgroundJobClient : IClient
    {
        #region Create
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
        #endregion

        #region Get
        /// <summary>
        /// Gets background job with <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public Task<IReadOnlyBackgroundJob> GetAsync(IClientConnection connection, string id, CancellationToken token = default);
        /// <summary>
        /// Gets background job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public async Task<IReadOnlyBackgroundJob> GetAsync(string id, CancellationToken token = default)
        {
            await using (var connection = await OpenConnectionAsync(HiveMindConstants.DefaultEnvironmentName, false, token).ConfigureAwait(false))
            {
                return await GetAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Gets background job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed</param>
        /// <param name="token"><param name="token">Optional token to cancel the request</param></param>
        /// <returns>Writeable version of background job with <paramref name="id"/></returns>
        public Task<ILockedBackgroundJob> GetWithLockAsync(IClientConnection connection, string id, string requester = null, CancellationToken token = default);
        /// <summary>
        /// Gets background job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Writeable version of background job with <paramref name="id"/></returns>
        public async Task<ILockedBackgroundJob> GetWithLockAsync(string id, string requester = null, CancellationToken token = default)
        {
            await using (var connection = await OpenConnectionAsync(HiveMindConstants.DefaultEnvironmentName, true, token).ConfigureAwait(false))
            {
                var job = await GetWithLockAsync(connection, id, requester, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        #endregion
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
