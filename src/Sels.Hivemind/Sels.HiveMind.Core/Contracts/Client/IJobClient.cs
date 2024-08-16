using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Query.Job;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Contains common methods for clients that interact with jobs.
    /// </summary>
    /// <typeparam name="TReadOnlyJob">The type used to return a readonly job</typeparam>
    /// <typeparam name="TLockedJob">The type used to return a locked job</typeparam>
    /// <typeparam name="TSortTarget">The type used to determine the sort order</typeparam>
    public interface IJobClient<TReadOnlyJob, TLockedJob, TSortTarget> : IClient
    {
        #region Get
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default)
            => GetAsync(connection.StorageConnection, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAsync([Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default) => GetAsync(HiveMindConstants.DefaultEnvironmentName, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public async Task<TReadOnlyJob> GetAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await GetAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAndTryLockAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default)
            => GetAndTryLockAsync(connection.StorageConnection, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAndTryLockAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAndTryLockAsync([Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default) => GetAndTryLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public async Task<TReadOnlyJob> GetAndTryLockAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await GetAndTryLockAsync(connection, id, requester, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Gets job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token"><param name="token">Optional token to cancel the request</param></param>
        /// <returns>Writeable version of job with <paramref name="id"/></returns>
        public Task<TLockedJob> GetWithLockAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default)
            => GetWithLockAsync(connection.StorageConnection, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token"><param name="token">Optional token to cancel the request</param></param>
        /// <returns>Writeable version of job with <paramref name="id"/></returns>
        public Task<TLockedJob> GetWithLockAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Writeable version of job with <paramref name="id"/></returns>
        public async Task<TLockedJob> GetWithLockAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default)
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
        /// Gets job with <paramref name="id"/> with a write lock.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Writeable version of job with <paramref name="id"/></returns>
        public Task<TLockedJob> GetWithLockAsync([Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default) => GetWithLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        #endregion

        #region TryGet
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default)
            => TryGetAsync(connection.StorageConnection, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAsync([Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default) => TryGetAsync(HiveMindConstants.DefaultEnvironmentName, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null/returns>
        public async Task<TReadOnlyJob> TryGetAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await TryGetAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free if the job exists.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/> by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAndTryLockAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default)
            => TryGetAndTryLockAsync(connection.StorageConnection, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free if the job exists.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/> by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAndTryLockAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free if the job exists.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAndTryLockAsync([Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default) => TryGetAndTryLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free if the job exists.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public async Task<TReadOnlyJob> TryGetAndTryLockAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, string requester, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await TryGetAndTryLockAsync(connection, id, requester, token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Searh
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
        public Task<IClientQueryResult<TReadOnlyJob>> SearchAsync(IClientConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
            => SearchAsync(connection.StorageConnection, conditionBuilder, pageSize, page, orderBy, orderByDescending, token);
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
        public Task<IClientQueryResult<TReadOnlyJob>> SearchAsync(IStorageConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default);
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
        public async Task<IClientQueryResult<TReadOnlyJob>> SearchAsync([Traceable(HiveLog.Environment)] string environment, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await SearchAsync(connection, conditionBuilder, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);
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
        public Task<IClientQueryResult<TReadOnlyJob>> SearchAsync(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        => SearchAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, pageSize, page, orderBy, orderByDescending, token);
        /// <summary>
        /// Queries background job amounts.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public Task<long> CountAsync(IClientConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
            => CountAsync(connection.StorageConnection, conditionBuilder, token);
        /// <summary>
        /// Queries background job amounts.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public Task<long> CountAsync(IStorageConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Queries background job amounts.
        /// </summary>
        /// <param name="environment">The HiveMind environment to query</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public async Task<long> CountAsync([Traceable(HiveLog.Environment)] string environment, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await CountAsync(connection, conditionBuilder, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Queries background job amounts.
        /// The default HiveMind environment will be queried.
        /// </summary>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many background jobs match the conditions</returns>
        public Task<long> CountAsync(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
        => CountAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, token);
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
        public Task<IClientQueryResult<TLockedJob>> SearchAndLockAsync(IClientConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
            => SearchAndLockAsync(connection.StorageConnection, conditionBuilder, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token);
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
        public Task<IClientQueryResult<TLockedJob>> SearchAndLockAsync(IStorageConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default);
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
        public async Task<IClientQueryResult<TLockedJob>> SearchAndLockAsync([Traceable(HiveLog.Environment)] string environment, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                var result = await SearchAndLockAsync(connection, conditionBuilder, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token).ConfigureAwait(false);

                //try
                //{
                //    await connection.CommitAsync(token).ConfigureAwait(false);
                //}
                //catch (Exception)
                //{
                //    await result.DisposeAsync().ConfigureAwait(false);
                //    throw;
                //}

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
        public Task<IClientQueryResult<TLockedJob>> SearchAndLockAsync(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder, int limit = HiveMindConstants.Query.MaxDequeueLimit, string requester = null, bool allowAlreadyLocked = false, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        => SearchAndLockAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token);
        #endregion

        /// <summary>
        /// Fetches locked jobs where the last heartbeat on the lock was longer than the configured timeout for the HiveMind environment.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="requester">Who is requesting the timed out jobs</param>
        /// <param name="limit">The maximum amount of locked jobs to return</param>
        /// <param name="token">Optiona token to cancel the request</param>
        /// <returns>The query result with the locked jobs</returns>
        public Task<IClientQueryResult<TLockedJob>> GetTimedOutAsync(IClientConnection connection, string requester, int limit = HiveMindConstants.Query.MaxDequeueLimit, CancellationToken token = default)
            => GetTimedOutAsync(connection.StorageConnection, requester, limit, token);
        /// <summary>
        /// Fetches locked jobs where the last heartbeat on the lock was longer than the configured timeout for the HiveMind environment.
        /// Locks on the fetches jobs should be set to <paramref name="requester"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="requester">Who is requesting the timed out jobs</param>
        /// <param name="limit">The maximum amount of locked jobs to return</param>
        /// <param name="token">Optiona token to cancel the request</param>
        /// <returns>The query result with the locked jobs</returns>
        public Task<IClientQueryResult<TLockedJob>> GetTimedOutAsync(IStorageConnection connection, string requester, int limit = HiveMindConstants.Query.MaxDequeueLimit, CancellationToken token = default);
        /// <summary>
        /// Returns all distinct queues being used by all jobs optionally that start with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="prefix">Optional filter on the queues to only include the queues that start with the prefix</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct job queues matching <paramref name="prefix"/> if defined or an empty array when there are no jobs</returns>
        public Task<string[]> GetAllQueuesAsync(IClientConnection connection, string? prefix = null, CancellationToken token = default)
            => GetAllQueuesAsync(connection.StorageConnection, prefix, token);
        /// <summary>
        /// Returns all distinct queues being used by all jobs optionally that start with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use to execute the request</param>
        /// <param name="prefix">Optional filter on the queues to only include the queues that start with the prefix</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct job queues matching <paramref name="prefix"/> if defined or an empty array when there are no jobs</returns>
        public Task<string[]> GetAllQueuesAsync(IStorageConnection connection, string? prefix = null, CancellationToken token = default);
    }
}
