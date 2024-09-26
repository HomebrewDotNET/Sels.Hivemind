using Sels.HiveMind.Colony;
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
    }
}
