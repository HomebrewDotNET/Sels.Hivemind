using Sels.HiveMind.Colony;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Colony;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Client for querying colony state.
    /// </summary>
    public interface IColonyClient : IClient
    {
        #region Get
        /// <summary>
        /// Tries to fetch the latest state of colony with <paramref name="id"/> from the storage.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state if it exists, otherwise null</returns>
        public Task<IColonyInfo?> TryGetColonyAsync(IStorageConnection connection, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default);
        /// <summary>
        /// Tries to fetch the latest state of colony with <paramref name="id"/> from the storage.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state if it exists, otherwise null</returns>
        public Task<IColonyInfo?> TryGetColonyAsync(IClientConnection connection, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default)
            => TryGetColonyAsync(Guard.IsNotNull(connection?.StorageConnection), id, token);
        /// <summary>
        /// Tries to fetch the latest state of colony with <paramref name="id"/> from the storage.
        /// </summary>
        /// <param name="environment">The environment to fetch the colony from</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state if it exists, otherwise null</returns>
        public async Task<IColonyInfo?> TryGetColonyAsync(string environment, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default)
        {
            environment = Guard.IsNotNullOrWhitespace(environment);
            HiveMindHelper.Validation.ValidateEnvironment(environment);
            id = Guard.IsNotNullOrWhitespace(id);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                return await TryGetColonyAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Tries to fetch the latest state of colony with <paramref name="id"/> from the storage in environment <see cref="HiveMindConstants.DefaultEnvironmentName"/>.
        /// </summary>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state if it exists, otherwise null</returns>
        public async Task<IColonyInfo?> TryGetColonyAsync([Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default)
        {
            id = Guard.IsNotNullOrWhitespace(id);

            return await TryGetColonyAsync(HiveMindConstants.DefaultEnvironmentName, id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetch the latest state of colony with <paramref name="id"/> from the storage.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state</returns>
        /// <exception cref="ColonyNotFoundException"></exception>
        public Task<IColonyInfo> GetColonyAsync(IStorageConnection connection, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default);
        /// <summary>
        /// Fetch the latest state of colony with <paramref name="id"/> from the storage.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state</returns>
        /// <exception cref="ColonyNotFoundException"></exception>
        public Task<IColonyInfo> GetColonyAsync(IClientConnection connection, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default)
            => GetColonyAsync(Guard.IsNotNull(connection?.StorageConnection), id, token);
        /// <summary>
        /// Fetch the latest state of colony with <paramref name="id"/> from the storage.
        /// </summary>
        /// <param name="environment">The environment to fetch the colony from</param>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state</returns>
        /// <exception cref="ColonyNotFoundException"></exception>
        public async Task<IColonyInfo> GetColonyAsync(string environment, [Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default)
        {
            environment = Guard.IsNotNullOrWhitespace(environment);
            HiveMindHelper.Validation.ValidateEnvironment(environment);
            id = Guard.IsNotNullOrWhitespace(id);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                return await GetColonyAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Fetch the latest state of colony with <paramref name="id"/> from the storage in environment <see cref="HiveMindConstants.DefaultEnvironmentName"/>.
        /// </summary>
        /// <param name="id">The id of the colony to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The colony state</returns>
        /// <exception cref="ColonyNotFoundException"></exception>
        public async Task<IColonyInfo> GetColonyAsync([Traceable(HiveLog.Colony.Id)] string id, CancellationToken token = default)
        {
            id = Guard.IsNotNullOrWhitespace(id);

            await using (var connection = await OpenConnectionAsync(HiveMindConstants.DefaultEnvironmentName, true, token).ConfigureAwait(false))
            {
                return await GetColonyAsync(connection, id, token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Search
        /// <summary>
        /// Queries colonies.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which colonies to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public Task<IClientQueryResult<IColonyInfo>> SearchAsync(IClientConnection connection, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryColonyOrderByTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
            => SearchAsync(connection.StorageConnection, conditionBuilder, pageSize, page, orderBy, orderByDescending, token);
        /// <summary>
        /// Queries colonies.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which colonies to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public Task<IClientQueryResult<IColonyInfo>> SearchAsync(IStorageConnection connection, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryColonyOrderByTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default);
        /// <summary>
        /// Queries colonies.
        /// </summary>
        /// <param name="environment">The HiveMind environment to query</param>
        /// <param name="conditionBuilder">Option builder for limiting which colonies to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public async Task<IClientQueryResult<IColonyInfo>> SearchAsync([Traceable(HiveLog.Environment)] string environment, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryColonyOrderByTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await SearchAsync(connection, conditionBuilder, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Queries colonies.
        /// The default HiveMind environment will be queried.
        /// </summary>
        /// <param name="conditionBuilder">Option builder for limiting which colonies to return</param>
        /// <param name="pageSize">The maximum amount of results to return per page</param>
        /// <param name="page">The result page to return</param>
        /// <param name="orderBy">Optional sort order</param>
        /// <param name="orderByDescending">True to order <paramref name="orderBy"/> descending, otherwise false for ascending</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The query result</returns>
        public Task<IClientQueryResult<IColonyInfo>> SearchAsync(Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, int pageSize = HiveMindConstants.Query.MaxResultLimit, int page = 1, QueryColonyOrderByTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        => SearchAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, pageSize, page, orderBy, orderByDescending, token);
        /// <summary>
        /// Queries colony amounts.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which colonies to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many colonies match the conditions</returns>
        public Task<long> CountAsync(IClientConnection connection, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
            => CountAsync(connection.StorageConnection, conditionBuilder, token);
        /// <summary>
        /// Queries colony amounts.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many colonies match the conditions</returns>
        public Task<long> CountAsync(IStorageConnection connection, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Queries colony amounts.
        /// </summary>
        /// <param name="environment">The HiveMind environment to query</param>
        /// <param name="conditionBuilder">Option builder for limiting which colonies to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many colonies match the conditions</returns>
        public async Task<long> CountAsync([Traceable(HiveLog.Environment)] string environment, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, false, token).ConfigureAwait(false))
            {
                return await CountAsync(connection, conditionBuilder, token).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Queries colony amounts.
        /// The default HiveMind environment will be queried.
        /// </summary>
        /// <param name="conditionBuilder">Option builder for limiting which jobs to count</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many colonies match the conditions</returns>
        public Task<long> CountAsync(Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
        => CountAsync(HiveMindConstants.DefaultEnvironmentName, conditionBuilder, token);
        #endregion
    }
}
