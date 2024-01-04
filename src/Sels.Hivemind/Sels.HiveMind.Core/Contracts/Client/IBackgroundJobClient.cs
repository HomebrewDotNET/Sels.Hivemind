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
using Sels.HiveMind.Query.Job;
using Sels.Core.Extensions;
using Sels.Core;

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
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync<T>(string environment, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync<T>(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class => CreateAsync<T>(HiveMindConstants.DefaultEnvironmentName, methodSelector, jobBuilder, token);
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
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync(string environment, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the created job</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) => CreateAsync(HiveMindConstants.DefaultEnvironmentName, methodSelector, jobBuilder, token);
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
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public Task<IReadOnlyBackgroundJob> GetAsync(string id, CancellationToken token = default) => GetAsync(HiveMindConstants.DefaultEnvironmentName, id, token);
        /// <summary>
        /// Gets background job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public async Task<IReadOnlyBackgroundJob> GetAsync(string environment, string id, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await GetAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Gets background job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public Task<IReadOnlyBackgroundJob> GetAndTryLockAsync(IClientConnection connection, string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets background job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public Task<IReadOnlyBackgroundJob> GetAndTryLockAsync(string id, string requester, CancellationToken token = default) => GetAndTryLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        /// <summary>
        /// Gets background job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of background job with <paramref name="id"/></returns>
        public async Task<IReadOnlyBackgroundJob> GetAndTryLockAsync(string environment, string id, string requester, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await GetAndTryLockAsync(connection, id, requester, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Gets background job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token"><param name="token">Optional token to cancel the request</param></param>
        /// <returns>Writeable version of background job with <paramref name="id"/></returns>
        public Task<ILockedBackgroundJob> GetWithLockAsync(IClientConnection connection, string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets background job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Writeable version of background job with <paramref name="id"/></returns>
        public async Task<ILockedBackgroundJob> GetWithLockAsync(string environment, string id, string requester, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await GetWithLockAsync(connection, id, requester, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Gets background job with <paramref name="id"/> with a write lock.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the background job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Writeable version of background job with <paramref name="id"/></returns>
        public Task<ILockedBackgroundJob> GetWithLockAsync(string id, string requester, CancellationToken token = default) => GetWithLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        #endregion

        #region Query
        /// <summary>
        /// Queries background jobs.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public Task<IClientQueryResult<IReadOnlyBackgroundJob>> QueryAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries background jobs.
        /// </summary>
        /// <param name="environment">The HiveMind environment to query</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public async Task<IClientQueryResult<IReadOnlyBackgroundJob>> QueryAsync(string environment, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await QueryAsync(connection, conditionBuilder, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Queries background jobs.
        /// The default HiveMind environment will be queried.
        /// </summary>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public Task<IClientQueryResult<IReadOnlyBackgroundJob>> QueryAsync(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        => QueryAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, pageSize, page, orderBy, orderByDescending, token);
        /// <summary>
        /// Queries background job amounts.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public Task<long> QueryCountAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Queries background job amounts.
        /// </summary>
        /// <param name="environment">The HiveMind environment to query</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public async Task<long> QueryCountAsync(string environment, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await QueryCountAsync(connection, conditionBuilder, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Queries background job amounts.
        /// The default HiveMind environment will be queried.
        /// </summary>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public Task<long> QueryCountAsync(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, CancellationToken token = default)
        => QueryCountAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, token);
        /// <summary>
        /// Dequeues the next <paramref name="limit"/> background jobs with a write lock matching the conditions defined by <paramref name="conditionBuilder"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="limit">The maximum amount of jobs to lock</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to return</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed</param>
        /// <param name="allowAlreadyLocked">If jobs already locked by <paramref name="requester"/> can be returned as well, otherwise false to return only jobs that weren't locked</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result with the locked background jobs</returns>
        public Task<IClientQueryResult<ILockedBackgroundJob>> DequeueAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Dequeues the next <paramref name="limit"/> background jobs with a write lock matching the conditions defined by <paramref name="conditionBuilder"/>.
        /// </summary>
        /// <param name="environment">The HiveMind environment to query</param>
        /// <param name="limit">The maximum amount of jobs to lock</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to return</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed</param>
        /// <param name="allowAlreadyLocked">If jobs already locked by <paramref name="requester"/> can be returned as well, otherwise false to return only jobs that weren't locked</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result with the locked background jobs</returns>
        public async Task<IClientQueryResult<ILockedBackgroundJob>> DequeueAsync(string environment, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var result = await DequeueAsync(connection, conditionBuilder, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token).ConfigureAwait(false);

                try
                {
                    await connection.CommitAsync(token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await result.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                return result;
            }
        }
        /// <summary>
        /// Dequeues the next <paramref name="limit"/> background jobs with a write lock matching the conditions defined by <paramref name="conditionBuilder"/>.
        /// The default HiveMind environment will be queried.
        /// </summary>
        /// <param name="limit">The maximum amount of jobs to lock</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to return</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed</param>
        /// <param name="allowAlreadyLocked">If jobs already locked by <paramref name="requester"/> can be returned as well, otherwise false to return only jobs that weren't locked</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result with the locked background jobs</returns>
        public Task<IClientQueryResult<ILockedBackgroundJob>> DequeueAsync(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        => DequeueAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token);
        #endregion

        /// <summary>
        /// Fetches locked background jobs where the last heartbeat on the lock was longer than the configured timeout for the HiveMind environment.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="requester">Who is requesting the timed out jobs</param>
        /// <param name="limit">The maximum amount of locked jobs to return</param>
        /// <param name="token">Optiona token to cancel the request</param>
        /// <returns>The query result with the locked background jobs</returns>
        public Task<IClientQueryResult<ILockedBackgroundJob>> GetTimedOutAsync(IClientConnection connection, string requester, int limit = HiveMindConstants.Query.MaxDequeueLimit, CancellationToken token = default);

        /// <summary>
        /// Returns all distinct queues being used by all background jobs.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct background job queues or an empty array when there are no background jobs</returns>
        public Task<string[]> GetAllQueuesAsync(IClientConnection connection, CancellationToken token = default);
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
        /// Places the job in queue <paramref name="queue"/> with a priority of <paramref name="priority"/>.
        /// </summary>
        /// <param name="queue">The queue to place the job in</param>
        /// <param name="priority">The priority of the job in <paramref name="priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder InQueue(string queue, QueuePriority priority = QueuePriority.Normal);
        /// <summary>
        /// Places the job in queue <see cref="HiveMindConstants.Queue.DefaultQueue"/> with a priority of <paramref name="priority"/>
        /// </summary>
        /// <param name="priority">The priority of the job in <paramref name="priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder WithPriority(QueuePriority priority) => InQueue(HiveMindConstants.Queue.DefaultQueue, priority);
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
        /// State will be changed to <see cref="EnqueuedState"/>.
        /// </summary>
        /// <param name="date">The date after which the job can be picked up</param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder DelayExecutionTo(DateTime date)
        {
            return InState(new EnqueuedState(date));
        }

        #region Awaiting
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStates"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStates"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="AwaitingState.DeleteOnOtherState"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, IEnumerable<string> validStates, bool deleteOnOtherState = false)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));

            return InState(new AwaitingState(jobId, validStates, deleteOnOtherState));
        }
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStates"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStates"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="AwaitingState.DeleteOnOtherState"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, BackgroundJobContinuationStates validStates, bool deleteOnOtherState = false)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));

            if(validStates.HasFlag(BackgroundJobContinuationStates.Any)) return EnqueueAfter(jobId, null, deleteOnOtherState);

            List<string> validStateNames = new List<string>();

            if (validStates.HasFlag(BackgroundJobContinuationStates.Succeeded)) validStateNames.Add(SucceededState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Failed)) validStateNames.Add(FailedState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Deleted)) validStateNames.Add(DeletedState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Executing)) validStateNames.Add(ExecutingState.StateName);

            return EnqueueAfter(jobId, validStateNames, deleteOnOtherState);
        }
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStateName"/> and <paramref name="validStateNames"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStateName"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="validStateNames"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="AwaitingState.DeleteOnOtherState"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, bool deleteOnOtherState, string validStateName, params string[] validStateNames)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));
            validStateName.ValidateArgumentNotNullOrWhitespace(nameof(validStateName));

            return EnqueueAfter(jobId, Helper.Collection.Enumerate(validStateName, validStateNames), deleteOnOtherState);
        }
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStateName"/> and <paramref name="validStateNames"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStateName"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="validStateNames"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, string validStateName, params string[] validStateNames)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));
            validStateName.ValidateArgumentNotNullOrWhitespace(nameof(validStateName));

            return EnqueueAfter(jobId, false, validStateName, validStateNames);
        }
        #endregion
    }

    /// <summary>
    /// Contains the states after which an awaiting job can transition into the <see cref="EnqueuedState"/>.
    /// </summary>
    [Flags]
    public enum BackgroundJobContinuationStates
    {
        /// <summary>
        /// Continue job if parent job transitions into any state.
        /// </summary>
        Any = 1,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="SucceededState"/>.
        /// </summary>
        Succeeded = 2,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="FailedState"/>.
        /// </summary>
        Failed = 4,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="DeletedState"/>.
        /// </summary>
        Deleted = 8,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="ExecutingState"/>.
        /// </summary>
        Executing = 16,
    }
}
