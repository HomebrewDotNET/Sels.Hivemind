﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Sels.HiveMind.Interval;
using System.Linq.Expressions;
using System.Threading;
using Sels.HiveMind.Storage;
using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Job.Recurring;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Client for creating, fetching and querying background jobs.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobClient : IJobClient<IReadOnlyRecurringJob, ILockedRecurringJob, QueryRecurringJobOrderByTarget?>
    {
        #region CreateOrUpdate
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync<T>(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, Expression<Func<T, object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
            => CreateOrUpdateAsync<T>(connection.ValidateArgument(nameof(connection)).StorageConnection, id, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync<T>(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, Expression<Func<T, object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class;
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public async Task<bool> CreateOrUpdateAsync<T>([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, Expression<Func<T, object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateOrUpdateAsync<T>(connection, id, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync<T>(string id, Expression<Func<T, object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
            => CreateOrUpdateAsync<T>(HiveMindConstants.DefaultEnvironmentName, id, methodSelector, jobBuilder, token);

        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync<T>(IClientConnection connection, string id, Expression<Action<T>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
            => CreateOrUpdateAsync<T>(connection.ValidateArgument(nameof(connection)).StorageConnection, id, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync<T>(IStorageConnection connection, string id, Expression<Action<T>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class;
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public async Task<bool> CreateOrUpdateAsync<T>([Traceable(HiveLog.Environment)] string environment, string id, Expression<Action<T>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateOrUpdateAsync<T>(connection, id, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> of type <typeparamref name="T"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync<T>(string id, Expression<Action<T>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
            => CreateOrUpdateAsync<T>(HiveMindConstants.DefaultEnvironmentName, id, methodSelector, jobBuilder, token);

        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, Expression<Func<object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
            => CreateOrUpdateAsync(connection.ValidateArgument(nameof(connection)).StorageConnection, id, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync(IStorageConnection connection, string id, Expression<Func<object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method on to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public async Task<bool> CreateOrUpdateAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, Expression<Func<object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateOrUpdateAsync(connection, id, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync([Traceable(HiveLog.Job.Id)] string id, Expression<Func<object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
            => CreateOrUpdateAsync(HiveMindConstants.DefaultEnvironmentName, id, methodSelector, jobBuilder, token);

        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, Expression<Action> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
            => CreateOrUpdateAsync(connection.ValidateArgument(nameof(connection)).StorageConnection, id, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, Expression<Action> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method on to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public async Task<bool> CreateOrUpdateAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, Expression<Action> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateOrUpdateAsync(connection, id, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates or updates recurring job <paramref name="id"/> that calls a static method.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the static method to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> CreateOrUpdateAsync([Traceable(HiveLog.Job.Id)] string id, Expression<Action> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
            => CreateOrUpdateAsync(HiveMindConstants.DefaultEnvironmentName, id, methodSelector, jobBuilder, token);
        #endregion

        #region Delete
        /// <summary>
        /// Permanently deletes recurring job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="requester">Who is requesting the deletion</param>
        /// <param name="pollingInterval">How often try and lock a recurring job if it's currently locked by another job</param>
        /// <param name="timeout">The maximum amount of time to wait for the job to be locked. When set to null no timeout will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> DeleteAsync(IClientConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester = null, TimeSpan? pollingInterval = null, TimeSpan? timeout = null, CancellationToken token = default)
            => DeleteAsync(connection.ValidateArgument(nameof(connection)).StorageConnection, id, requester, pollingInterval, timeout, token);
        /// <summary>
        /// Permanently deletes recurring job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="requester">Who is requesting the deletion</param>
        /// <param name="pollingInterval">How often try and lock a recurring job if it's currently locked by another job</param>
        /// <param name="timeout">The maximum amount of time to wait for the job to be locked. When set to null no timeout will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> DeleteAsync(IStorageConnection connection, [Traceable(HiveLog.Job.Id)] string id, string requester = null, TimeSpan? pollingInterval = null, TimeSpan? timeout = null, CancellationToken token = default);
        /// <summary>
        /// Permanently deletes recurring job with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="environment">The environment to delete the job from</param>
        /// <param name="requester">Who is requesting the deletion</param>
        /// <param name="pollingInterval">How often try and lock a recurring job if it's currently locked by another job</param>
        /// <param name="timeout">The maximum amount of time to wait for the job to be locked. When set to null no timeout will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public async Task<bool> DeleteAsync([Traceable(HiveLog.Environment)] string environment, [Traceable(HiveLog.Job.Id)] string id, string requester = null, TimeSpan? pollingInterval = null, TimeSpan? timeout = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await DeleteAsync(connection, id, requester, pollingInterval, timeout, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }

        /// <summary>
        /// Permanently deletes recurring job with <paramref name="id"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <param name="id">The unique id of the recurring job</param>
        /// <param name="requester">Who is requesting the deletion</param>
        /// <param name="pollingInterval">How often try and lock a recurring job if it's currently locked by another job</param>
        /// <param name="timeout">The maximum amount of time to wait for the job to be locked. When set to null no timeout will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the recurring job was created, otherwise false if updated</returns>
        public Task<bool> DeleteAsync([Traceable(HiveLog.Job.Id)] string id, string requester = null, TimeSpan? pollingInterval = null, TimeSpan? timeout = null, CancellationToken token = default)
        => DeleteAsync(HiveMindConstants.DefaultEnvironmentName, id, requester, pollingInterval, timeout, token);
        #endregion
    }
}
