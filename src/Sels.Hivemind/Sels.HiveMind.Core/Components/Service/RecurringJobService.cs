using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Templates.Service;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Service
{
    /// <inheritdoc cref="IRecurringJobService"/>
    public class RecurringJobService : BaseJobService, IRecurringJobService
    {
        // Fields
        private readonly RecurringJobValidationProfile _recurringJobValidationProfile;

        /// <inheritdoc cref="BackgroundJobService"/>
        /// <param name="recurringJobValidationProfile">Used to validate recurring job state</param>
        /// <param name="options"><inheritdoc cref="BaseJobService._options"/></param>
        /// <param name="cache"><inheritdoc cref="BaseJobService._options"/></param>
        /// <param name="logger"><inheritdoc cref="BaseJobService._logger"/></param>
        public RecurringJobService(RecurringJobValidationProfile recurringJobValidationProfile, IOptionsSnapshot<HiveMindOptions> options, IMemoryCache cache, ILogger<BackgroundJobService> logger = null) : base(options, cache, logger)
        {
            _recurringJobValidationProfile = recurringJobValidationProfile.ValidateArgument(nameof(recurringJobValidationProfile));
        }

        /// <inheritdoc/>
        public async Task<RecurringJobStorageData> TryCreateAsync(IStorageConnection connection, RecurringJobConfigurationStorageData storageData, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            storageData.ValidateArgument(nameof(storageData));

            _logger.Log($"Trying to create recurring job <{HiveLog.Job.Id}> in environment {HiveLog.Environment}", storageData.Id, connection.Environment);
            var result = await _recurringJobValidationProfile.ValidateAsync(storageData, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(storageData);

            var recurringJobState = await RunTransaction(connection, () => connection.Storage.TryCreateAsync(connection, storageData, token), token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Expected recurring job instance from storage but got null");

            if (recurringJobState.States.HasValue())
            {
                _logger.Log($"Could not create recurring job <{HiveLog.Job.Id}> in environment {HiveLog.Environment} because it already exists", storageData.Id, connection.Environment);
            }
            else
            {
                _logger.Log($"Created recurring job <{HiveLog.Job.Id}> in environment {HiveLog.Environment}", storageData.Id, connection.Environment);
            }

            return recurringJobState;
        }
        /// <inheritdoc/>
        public async Task TryUpdateAsync(IStorageConnection connection, RecurringJobStorageData recurringJob, bool releaseLock, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            recurringJob.ValidateArgument(nameof(recurringJob));

            // Validate storage data
            var result = await _recurringJobValidationProfile.ValidateAsync(recurringJob, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(recurringJob);

            await RunTransaction(connection, async () =>
            {
                _logger.Log($"Updating recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", recurringJob.Id, connection.Environment);
                ValidateLock(recurringJob, connection.Environment);
                var wasUpdated = await connection.Storage.TryUpdateRecurringJobAsync(connection, recurringJob, releaseLock, token).ConfigureAwait(false);
                if (!wasUpdated) throw new JobLockStaleException(recurringJob.Id, connection.Environment);
                _logger.Log($"Updated recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", recurringJob.Id, connection.Environment);
            }, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public IRecurringJobState ConvertToState(RecurringJobStateStorageData stateData, string environment)
        {
            stateData.ValidateArgument(nameof(stateData));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            _logger.Log($"Converting state storage format into a new instance of <{stateData.OriginalTypeName}>");

            var constructor = GetStateConverter<IRecurringJobState>(stateData, environment);

            var options = _options.Get(environment);

            var properties = new LazyPropertyInfoDictionary(options, _cache);
            if (stateData.Properties.HasValue()) stateData.Properties.Execute(x => properties.Add(x.Name, new LazyPropertyInfo(x, options, _cache)));
            properties.Add(nameof(IRecurringJobState.Sequence), new LazyPropertyInfo(stateData.Sequence, options, _cache));
            properties.Add(nameof(IRecurringJobState.ElectedDateUtc), new LazyPropertyInfo(stateData.ElectedDateUtc, options, _cache));
            properties.Add(nameof(IRecurringJobState.Reason), new LazyPropertyInfo(stateData.Reason, options, _cache));
            properties.Add(nameof(IRecurringJobState.Name), new LazyPropertyInfo(stateData.Name, options, _cache));

            var state = constructor(properties);
            state.Sequence = stateData.Sequence;
            return state;
        }
        /// <inheritdoc/>
        public IEnumerable<StorageProperty> GetStorageProperties(IRecurringJobState state, string environment)
        {
            state.ValidateArgument(nameof(state));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            _logger.Log($"Fetching all public properties from <{state}>");

            var options = _options.Get(environment);
            var getter = GetPropertyFetcher<IRecurringJobState>(state, environment);

            var dictionary = new Dictionary<string, object>();
            getter(state, dictionary);
            dictionary.Remove(nameof(IRecurringJobState.Sequence));
            dictionary.Remove(nameof(IRecurringJobState.ElectedDateUtc));
            dictionary.Remove(nameof(IRecurringJobState.Reason));
            dictionary.Remove(nameof(IRecurringJobState.Name));

            return dictionary.Select(x => new StorageProperty(x.Key, x.Value, options, _cache));
        }
        /// <inheritdoc/>
        public async Task<bool> TryLockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            var wasLocked = await TryLockIfExistsAsync(id, connection, requester, token).ConfigureAwait(false);

            if (!wasLocked.HasValue)
            {
                _logger.Warning($"Recurring job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new JobNotFoundException(id, connection.Environment);
            }

            return wasLocked.Value;
        }
        /// <inheritdoc/>
        public async Task<bool?> TryLockIfExistsAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to acquire lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for requester <{requester}>", id, connection.Environment);

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryLockRecurringJobAsync(connection, id, requester, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Log($"Recurring job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                return null;
            }

            if (requester.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, requester);
                return true;
            }
            else
            {
                _logger.Log($"Recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> could not be locked by <{requester}> because it is already locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);
                return false;
            }
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Setting lock heartbeat on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{holder}>", id, connection.Environment);

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryHeartbeatLockOnRecurringJobAsync(connection, id, holder, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Recurring job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new JobNotFoundException(id, connection.Environment);
            }

            if (holder.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Lock heartbeat on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> has been set to <{jobLock.LockHeartbeatUtc}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
                return jobLock;
            }
            else
            {
                _logger.Warning($"Lock heartbeat on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> could not be set for <{holder}> because it is already locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);
                throw new JobAlreadyLockedException(id, connection.Environment, holder, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> LockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to acquire lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for requester <{requester}>", id, connection.Environment);

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryLockRecurringJobAsync(connection, id, requester, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Recurring job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new JobNotFoundException(id, connection.Environment);
            }

            if (requester.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, requester);
                return jobLock;
            }
            else
            {
                _logger.Warning($"Recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> could not be locked by <{requester}> because it is already locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);
                throw new JobAlreadyLockedException(id, connection.Environment, requester, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public Task<long> CountAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task CreateActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<bool> DeleteActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public async Task<RecurringJobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching recurring job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);

            var job = await connection.Storage.GetRecurringJobAsync(id, connection, token).ConfigureAwait(false);

            if (job == null)
            {
                _logger.Warning($"Recurring job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new JobNotFoundException(id, connection.Environment);
            }

            _logger.Log($"Fetched recurring job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);
            return job;
        }
        /// <inheritdoc/>
        public Task<ActionInfo[]> GetNextActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }      
        /// <inheritdoc/>
        public Task<RecurringJobStorageData[]> GetTimedOutJobs(IStorageConnection connection, int limit, string requester, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<(RecurringJobStorageData[] Results, long Total)> SearchAndLockAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<(RecurringJobStorageData[] Results, long Total)> SearchAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task SetDataAsync<T>(IStorageConnection connection, string id, string name, T value, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<(bool Exists, T Data)> TryGetDataAsync<T>(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }   
    }
}
