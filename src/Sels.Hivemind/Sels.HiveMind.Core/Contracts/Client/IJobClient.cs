using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Contains common methods for clients that interact with jobs.
    /// </summary>
    /// <typeparam name="TReadOnlyJob">The type used to return a readonly job</typeparam>
    /// <typeparam name="TLockedJob">The type used to return a locked job</typeparam>
    public interface IJobClient<TReadOnlyJob, TLockedJob> : IClient
    {
        #region Get
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAsync(IClientConnection connection, string id, CancellationToken token = default)
            => GetAsync(connection.StorageConnection, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAsync(IStorageConnection connection, string id, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAsync(string id, CancellationToken token = default) => GetAsync(HiveMindConstants.DefaultEnvironmentName, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public async Task<TReadOnlyJob> GetAsync(string environment, string id, CancellationToken token = default)
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
        public Task<TReadOnlyJob> GetAndTryLockAsync(IClientConnection connection, string id, string requester, CancellationToken token = default)
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
        public Task<TReadOnlyJob> GetAndTryLockAsync(IStorageConnection connection, string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public Task<TReadOnlyJob> GetAndTryLockAsync(string id, string requester, CancellationToken token = default) => GetAndTryLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/></returns>
        public async Task<TReadOnlyJob> GetAndTryLockAsync(string environment, string id, string requester, CancellationToken token = default)
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
        public Task<TLockedJob> GetWithLockAsync(IClientConnection connection, string id, string requester, CancellationToken token = default)
            => GetWithLockAsync(connection.StorageConnection, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token"><param name="token">Optional token to cancel the request</param></param>
        /// <returns>Writeable version of job with <paramref name="id"/></returns>
        public Task<TLockedJob> GetWithLockAsync(IStorageConnection connection, string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> with a write lock.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Writeable version of job with <paramref name="id"/></returns>
        public async Task<TLockedJob> GetWithLockAsync(string environment, string id, string requester, CancellationToken token = default)
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
        public Task<TLockedJob> GetWithLockAsync(string id, string requester, CancellationToken token = default) => GetWithLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        #endregion

        #region TryGet
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAsync(IClientConnection connection, string id, CancellationToken token = default)
            => TryGetAsync(connection.StorageConnection, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAsync(IStorageConnection connection, string id, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAsync(string id, CancellationToken token = default) => TryGetAsync(HiveMindConstants.DefaultEnvironmentName, id, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> if it exists.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null/returns>
        public async Task<TReadOnlyJob> TryGetAsync(string environment, string id, CancellationToken token = default)
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
        public Task<TReadOnlyJob> TryGetAndTryLockAsync(IClientConnection connection, string id, string requester, CancellationToken token = default)
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
        public Task<TReadOnlyJob> TryGetAndTryLockAsync(IStorageConnection connection, string id, string requester, CancellationToken token = default);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free if the job exists.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// Fetches from the default HiveMind environment.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public Task<TReadOnlyJob> TryGetAndTryLockAsync(string id, string requester, CancellationToken token = default) => TryGetAndTryLockAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, token);
        /// <summary>
        /// Gets job with <paramref name="id"/> and will try to lock if it's free if the job exists.
        /// Writeable job can be acquired by calling <see cref="IReadOnlyBackgroundJob.LockAsync(string, CancellationToken)"/> if locking was successful by checking <see cref="IReadOnlyBackgroundJob.HasLock"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="environment">The HiveMind environment to fetch from</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used. If the job is already locked by the same requester, the lock will be refreshed if expiry date is close to the configured safety offset</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Read only version of job with <paramref name="id"/> if it exists, otherwise null</returns>
        public async Task<TReadOnlyJob> TryGetAndTryLockAsync(string environment, string id, string requester, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await TryGetAndTryLockAsync(connection, id, requester, token).ConfigureAwait(false);
            }
        }
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
        /// Returns all distinct queues being used by all jobs.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct job queues or an empty array when there are no jobs</returns>
        public Task<string[]> GetAllQueuesAsync(IClientConnection connection, CancellationToken token = default)
            => GetAllQueuesAsync(connection.StorageConnection, token);
        /// <summary>
        /// Returns all distinct queues being used by all jobs.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>An array with all distinct job queues or an empty array when there are no jobs</returns>
        public Task<string[]> GetAllQueuesAsync(IStorageConnection connection, CancellationToken token = default);
    }
}
