using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Colony;
using Sels.HiveMind.Templates.Service;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;

namespace Sels.HiveMind.Service
{
    /// <inheritdoc cref="IColonyService"/>
    public class ColonyService : BaseService, IColonyService
    {
        // Fields
        private readonly IStorageProvider _storageProvider;
        private readonly ColonyValidationProfile _profile;

        // Fields
        /// <inheritdoc cref="ColonyService"/>
        /// <param name="storageProvider">Used to open connections/transactions to the colony storage</param>
        /// <param name="options"><inheritdoc cref="BaseService._options"/></param>
        /// <param name="cache"><inheritdoc cref="BaseService._cache"/></param>
        /// <param name="logger"><inheritdoc cref="BaseService._logger"/></param>
        public ColonyService(IStorageProvider storageProvider, ColonyValidationProfile profile, IOptionsMonitor<HiveMindOptions> options, IMemoryCache cache, ILogger? logger = null) : base(options, cache, logger)
        {
            _storageProvider = Guard.IsNotNull(storageProvider);
            _profile = Guard.IsNotNull(profile);
        }
        /// <inheritdoc />
        public async Task ReleaseLockAndSyncStateIfHeldByAsync(IColonyInfo colony, IReadOnlyDictionary<string, IEnumerable<LogEntry>> newDaemonLogs, [Traceable("HiveMind.Colony.Holder", null)] string holder, CancellationToken token = default)
        {
            colony = Guard.IsNotNull(colony);
            newDaemonLogs = Guard.IsNotNull(newDaemonLogs);
            holder = Guard.IsNotNullOrWhitespace(holder);

            if(await TrySyncStateAsync(colony, newDaemonLogs, holder, token))
            {
                await ReleaseLockIfHeldByAsync(colony, holder, token);
            }
            else
            {
                _logger.Warning($"Sync of state failed so lock is most likely stale. Not triggering unlock");
            }
        }
        private async Task ReleaseLockIfHeldByAsync(IColonyInfo colony, string requester, CancellationToken token = default)
        {
            colony = Guard.IsNotNull(colony);
            var colonyId = Guard.IsNotNullOrWhitespace(colony.Id);
            var environment = Guard.IsNotNullOrWhitespace(colony.Environment);
            requester = Guard.IsNotNullOrWhitespace(requester);

            _logger.Log($"Releasing lock on colony if it is still held by <{HiveLog.Colony.HolderParam}>", requester);

            var wasReleased = await RunTransaction(environment, x => x.Storage.ReleaseLockOnColonyIfHeldByAsync(x, colonyId, requester, token), token).ConfigureAwait(false);

            if (wasReleased)
            {
                _logger.Log($"Lock on colony was released by <{HiveLog.Colony.HolderParam}>", requester);
            }
            else
            {
                _logger.Warning($"Could not release lock on colony for <{HiveLog.Colony.HolderParam}>", requester);
            }
        }
        /// <inheritdoc />
        public async Task<(bool HeartbeatWasSet, ILockInfo Lock)> TryHeartbeatProcessLockAsync(IColonyInfo colony, string requester, CancellationToken token = default)
        {
            colony = Guard.IsNotNull(colony);
            var colonyId = Guard.IsNotNullOrWhitespace(colony.Id);
            var environment = Guard.IsNotNullOrWhitespace(colony.Environment);
            requester = Guard.IsNotNullOrWhitespace(requester);

            _logger.Log($"Heartbeating lock on colony if it is still held by <{HiveLog.Colony.HolderParam}>", requester);

            var result = await RunTransaction(environment, x => x.Storage.TryHeartbeatProcessLockOnColonyAsync(x, colonyId, requester, token), token).ConfigureAwait(false);

            if (result.HeartbeatWasSet)
            {
                _logger.Log($"Set heartbeat on lock on colony for <{HiveLog.Colony.HolderParam}>", requester);
            }
            else
            {
                _logger.Warning($"Could not set heartbeat on lock on colony for <{HiveLog.Colony.HolderParam}>", requester);
            }

            return result;
        }
        /// <inheritdoc />
        public async Task<(bool WasLocked, ILockInfo Lock)> TrySyncAndGetProcessLockAsync(IColonyInfo colony, string requester, CancellationToken token = default)
        {
            colony = Guard.IsNotNull(colony);
            requester = Guard.IsNotNullOrWhitespace(requester);

            _logger.Log($"Trying to sync and get process lock on colony <{HiveLog.Colony.IdParam}> for requester <{requester}>", colony.Id);
            
            // Validate
            _logger.Debug($"Converting colony to storage format and validating");
            var storageFormat = new ColonyStorageData(colony, _options.CurrentValue, _cache);
            var result = await _profile.ValidateAsync(storageFormat);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(colony);

            // Try sync and lock
            var environment = Guard.IsNotNullOrWhitespace(colony.Environment);
            var options = _options.Get(environment);
            var timeout = options.LockTimeout + options.LockExpirySafetyOffset + colony.Options.DaemonMaxStopTime + colony.Options.ReleaseLockTime; // Should ensure nodes have plenty of time stop in case of storage issues before other nodes can acquire the lock
            var lockResult = await RunTransaction(environment, x => x.Storage.TrySyncAndGetProcessLockOnColonyAsync(x, storageFormat, requester, timeout, token), token).ConfigureAwait(false);

            if(lockResult.WasLocked)
            {
                _logger.Log($"Successfully locked colony <{HiveLog.Colony.IdParam}> for requester <{HiveLog.Colony.HolderParam}>", colony.Id, requester);
            }
            else
            {
                _logger.Warning($"Could not lock colony <{HiveLog.Colony.IdParam}> for requester <{requester}>", colony.Id);
            }

            return lockResult;
        }
        /// <inheritdoc />
        public async Task<bool> TrySyncStateAsync(IColonyInfo colony, IReadOnlyDictionary<string, IEnumerable<LogEntry>> newDaemonLogs, [Traceable("HiveMind.Colony.Holder", null)] string holder, CancellationToken token = default)
        {
            colony = Guard.IsNotNull(colony);
            newDaemonLogs = Guard.IsNotNull(newDaemonLogs);
            holder = Guard.IsNotNullOrWhitespace(holder);

            _logger.Log($"Trying to sync state of colony <{HiveLog.Colony.IdParam}> for holder <{HiveLog.Colony.HolderParam}>", colony.Id, holder);

            // Validate
            _logger.Debug($"Converting colony to storage format and validating");
            var storageFormat = new ColonyStorageData(colony, _options.CurrentValue, _cache);
            storageFormat.Daemons.Execute(x => x.NewLogEntries = newDaemonLogs.TryGetValue(x.Name, out var logs) && logs.HasValue() ? logs.ToList() : x.NewLogEntries);
            var result = await _profile.ValidateAsync(storageFormat);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(colony);

            // Try sync
            var lockResult = await RunTransaction(colony.Environment, x => x.Storage.TrySyncColonyAsync(x, storageFormat, holder, token), token).ConfigureAwait(false);

            if (lockResult)
            {
                _logger.Log($"Successfully synced colony state <{HiveLog.Colony.IdParam}> for holder <{HiveLog.Colony.HolderParam}>", colony.Id, holder);
            }
            else
            {
                _logger.Warning($"Could not sync colony colony <{HiveLog.Colony.IdParam}> for holder <{HiveLog.Colony.HolderParam}>", colony.Id, holder);
            }

            return lockResult;
        }

        /// <summary>
        /// Makes sure <paramref name="action"/> is executed using a transaction started by opening a connection to environment <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The HiveMind environment to open the connection towards</param>
        /// <param name="action">The action to execute in the transaction scope</param>
        /// <param name="token">Optional token to cancel the request</param>
        protected async Task RunTransaction(string environment, AsyncAction<IStorageConnection> action, CancellationToken token)
        {
            environment.ValidateArgument(nameof(environment));
            action.ValidateArgument(nameof(action));

            _logger.Debug($"Creating storage for environment <{HiveLog.EnvironmentParam}>", environment);
            await using var storageScope = await _storageProvider.CreateAsync(environment, token).ConfigureAwait(false);
            _logger.Debug($"Opening connection with transactions in environment <{HiveLog.EnvironmentParam}>");
            await using var connection = await storageScope.Component.OpenConnectionAsync(false, token).ConfigureAwait(false);

            await RunTransaction(connection, () => action(connection), token);
        }
        /// <summary>
        /// Makes sure <paramref name="action"/> is executed using a transaction started  by opening a connection to environment <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The HiveMind environment to open the connection towards</param>
        /// <param name="action">The action to execute in the transaction scope</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The value returned by calling <paramref name="action"/></returns>
        protected async Task<T> RunTransaction<T>(string environment, AsyncFunc<IStorageConnection, T> action, CancellationToken token)
        {
            environment.ValidateArgument(nameof(environment));
            action.ValidateArgument(nameof(action));

            _logger.Debug($"Creating storage for environment <{HiveLog.EnvironmentParam}>", environment);
            await using var storageScope = await _storageProvider.CreateAsync(environment, token).ConfigureAwait(false);
            _logger.Debug($"Opening connection with transactions in environment <{HiveLog.EnvironmentParam}>");
            await using var connection = await storageScope.Component.OpenConnectionAsync(false, token).ConfigureAwait(false);

            return await RunTransaction<T>(connection, () => action(connection), token);
        }
    }
}
