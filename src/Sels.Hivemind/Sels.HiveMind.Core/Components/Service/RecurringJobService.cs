using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Storage.Job.Recurring;
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
        private readonly QueryValidationProfile _jobQueryValidationProfile;

        /// <inheritdoc cref="BackgroundJobService"/>
        /// <param name="recurringJobValidationProfile">Used to validate recurring job state</param>
        /// <param name="jobQueryValidationProfile">Used to validate query parameters</param>
        /// <param name="options"><inheritdoc cref="BaseJobService._options"/></param>
        /// <param name="cache"><inheritdoc cref="BaseJobService._options"/></param>
        /// <param name="logger"><inheritdoc cref="BaseJobService._logger"/></param>
        public RecurringJobService(RecurringJobValidationProfile recurringJobValidationProfile, QueryValidationProfile jobQueryValidationProfile, IOptionsMonitor<HiveMindOptions> options, IMemoryCache cache, ILogger<BackgroundJobService>? logger = null) : base(options, cache, logger)
        {
            _recurringJobValidationProfile = recurringJobValidationProfile.ValidateArgument(nameof(recurringJobValidationProfile));
            _jobQueryValidationProfile = jobQueryValidationProfile.ValidateArgument(nameof(jobQueryValidationProfile));
        }

        /// <inheritdoc/>
        public async Task<RecurringJobStorageData> TryCreateAsync(IStorageConnection connection, RecurringJobConfigurationStorageData storageData, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            storageData.ValidateArgument(nameof(storageData));

            _logger.Log($"Trying to create recurring job <{HiveLog.Job.IdParam}> in environment {HiveLog.EnvironmentParam}", storageData.Id, connection.Environment);
            var result = await _recurringJobValidationProfile.ValidateAsync(storageData, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(storageData);

            var recurringJobState = await RunTransaction(connection, () => connection.Storage.TryCreateAsync(connection, storageData, token), token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Expected recurring job instance from storage but got null");

            if (recurringJobState.States.HasValue())
            {
                _logger.Log($"Could not create recurring job <{HiveLog.Job.IdParam}> in environment {HiveLog.EnvironmentParam} because it already exists", storageData.Id, connection.Environment);
            }
            else
            {
                _logger.Log($"Created recurring job <{HiveLog.Job.IdParam}> in environment {HiveLog.EnvironmentParam}", storageData.Id, connection.Environment);
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
                _logger.Log($"Updating recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", recurringJob.Id, connection.Environment);
                ValidateLock(recurringJob, connection.Environment);
                var wasUpdated = await connection.Storage.TryUpdateRecurringJobAsync(connection, recurringJob, releaseLock, token).ConfigureAwait(false);
                if (!wasUpdated) throw new JobLockStaleException(recurringJob.Id, connection.Environment);
                _logger.Log($"Updated recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", recurringJob.Id, connection.Environment);
            }, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public IRecurringJobState ConvertToState(JobStateStorageData stateData, string environment)
        {
            stateData.ValidateArgument(nameof(stateData));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            _logger.Log($"Converting state storage format into a new instance of <{stateData.OriginalTypeName}>");

            var constructor = GetStateConverter<IRecurringJobState>(stateData, environment);

            var options = _options.Get(environment);

            var properties = new LazyPropertyInfoDictionary(options, _cache);
            //if (stateData.Properties.HasValue()) stateData.Properties.Execute(x => properties.Add(x.Name, new LazyPropertyInfo(x, options, _cache)));
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
        public async Task<RecurringJobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            var (_, job) = await FetchAsync(id, connection, Guid.NewGuid().ToString(), false, token).ConfigureAwait(false);
            if (job == null) throw new JobNotFoundException(id, connection.Environment);
            return job;
        }
        /// <inheritdoc/>
        public async Task<(bool WasLocked, RecurringJobStorageData? Data)> FetchAsync(string id, IStorageConnection connection, string requester, bool tryLock, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            bool wasLocked = false;
            RecurringJobStorageData? job = null;

            if (tryLock)
            {
                _logger.Debug($"Trying to fetch recurringjob job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with a lock for <{requester}>", id, connection.Environment);
                (wasLocked, job) = await RunTransaction(connection, async () => await connection.Storage.TryLockAndTryGetRecurringJobAsync(id, requester, connection, token).ConfigureAwait(false), token).ConfigureAwait(false);
            }
            else
            {
                _logger.Debug($"Lock is not required on recurringjob job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>. Doing normal fetch", id, connection.Environment);
                job = await connection.Storage.GetRecurringJobAsync(id, connection, token).ConfigureAwait(false);
            }

            if (job == null)
            {
                _logger.Warning($"Background job <{HiveLog.Job.IdParam}> does not exist in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            }

            if (wasLocked)
            {
                _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with lock for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, requester);
            }
            else
            {
                _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            }

            return (wasLocked, job);
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Setting lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{holder}>", id, connection.Environment);

            var (wasLocked, jobLock) = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryHeartbeatLockOnRecurringJobAsync(connection, id, holder, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Recurring job <{HiveLog.Job.IdParam}> does not exist in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                throw new JobNotFoundException(id, connection.Environment);
            }

            if (wasLocked)
            {
                _logger.Log($"Lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> has been set to <{jobLock.LockHeartbeatUtc}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
                return jobLock;
            }
            else
            {
                _logger.Warning($"Lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> could not be set for <{holder}> because it is already locked by <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, jobLock.LockedBy);
                throw new JobAlreadyLockedException(id, connection.Environment, holder, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task CreateActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            _logger.Log($"Creating action of type <{action.Type}> for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);

            // Validate
            var result = await _recurringJobValidationProfile.ValidateAsync(action, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(action);

            // Execute
            await RunTransaction(connection, () => connection.Storage.CreateRecurringJobActionAsync(connection, action, token), token).ConfigureAwait(false);

            _logger.Log($"Created action <{action.Id}> of type <{action.Type}> for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);

        }
        /// <inheritdoc/>
        public async Task<bool> DeleteActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));

            _logger.Log($"Removing action <{id}> in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            var wasDeleted = await RunTransaction(connection, () => connection.Storage.DeleteRecurringJobActionByIdAsync(connection, id, token), token).ConfigureAwait(false);

            if (wasDeleted)
            {
                _logger.Log($"Removed action <{id}> in environment <{HiveLog.EnvironmentParam}>", connection.Environment);
            }
            else
            {
                _logger.Warning($"Could not remove action <{id}> in environment <{HiveLog.EnvironmentParam}>", connection.Environment);
            }

            return wasDeleted;
        }
        /// <inheritdoc/>
        public async Task<ActionInfo[]> GetNextActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Fetching the next <{limit}> actions for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if they exists", id, connection.Environment);

            var actions = await RunTransaction(connection, () => connection.Storage.GetNextRecurringJobActionsAsync(connection, id, limit, token), token).ConfigureAwait(false) ?? Array.Empty<ActionInfo>();

            _logger.Log($"Fetched <{actions.Length}> actions for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            return actions;
        }      
        /// <inheritdoc/>
        public async Task<RecurringJobStorageData[]> GetTimedOutJobs(IStorageConnection connection, int limit, string requester, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgument(nameof(requester));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Checking if there are timed out recurring jobs in environment <{HiveLog.EnvironmentParam}> which will be locked for <{requester}>", connection.Environment);

            var options = _options.Get(connection.Environment);

            var jobs = await RunTransaction(connection, () => connection.Storage.GetTimedOutRecurringJobs(connection, limit, requester, options.LockTimeout, token), token).ConfigureAwait(false);

            if (jobs.HasValue())
            {
                _logger.Log($"Got <{jobs.Length}> timed out recurring jobs in environment <{HiveLog.EnvironmentParam}> to lock for <{requester}>", connection.Environment);
                return jobs;
            }
            else
            {
                _logger.Log($"No timed out recurring jobs in environment <{HiveLog.EnvironmentParam}> to lock for <{requester}>", connection.Environment);
                return Array.Empty<RecurringJobStorageData>();
            }
        }
        /// <inheritdoc/>
        public async Task<RecurringJobStorageData[]> SearchAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Searching for recurring jobs in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _jobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await connection.Storage.SearchRecurringJobsAsync(connection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            _logger.Log($"Search for recurring jobs in environment <{HiveLog.EnvironmentParam}> returned <{result.Length}> jobs", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task<long> CountAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));

            _logger.Log($"Searching for an amount of recurring jobs in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _jobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.CountRecurringJobsAsync(connection, queryConditions, token), token).ConfigureAwait(false);

            _logger.Log($"Search for an amount of recurring jobs in environment <{HiveLog.EnvironmentParam}> returned <{result}> matching", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task<RecurringJobStorageData[]> SearchAndLockAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to lock the next <{limit}> recurring jobs in environment <{HiveLog.EnvironmentParam}> for <{requester}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _jobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.LockRecurringJobsAsync(connection, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token), token).ConfigureAwait(false);

            _logger.Log($"<{result.Length}> recurring jobs in environment <{HiveLog.EnvironmentParam}> are now locked by <{HiveLog.Job.LockHolderParam}>", connection.Environment, requester);
            return result;
        }

        /// <inheritdoc/>
        public async Task SetDataAsync<T>(IStorageConnection connection, string id, string name, T value, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            _logger.Log($"Saving data <{name}> to recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            _logger.Debug($"Converting data <{name}> of type <{value!.GetType()}> for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for storage", id, connection.Environment);

            var converted = HiveMindHelper.Storage.ConvertToStorageFormat(value, _options.Get(connection.Environment), _cache)!;

            await RunTransaction(connection, () => connection.Storage.SetRecurringJobDataAsync(connection, id, name, converted, token), token).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
        }
        /// <inheritdoc/>
        public async Task<(bool Exists, T? Data)> TryGetDataAsync<T>(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgument(nameof(name));

            _logger.Log($"Trying to fetch data <{name}> from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            if (await connection.Storage.TryGetRecurringJobDataAsync(connection, id, name, token).ConfigureAwait(false) is (true, var data))
            {
                _logger.Debug($"Fetched data <{name}> from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Converting to <{typeof(T)}>", id, connection.Environment);
                var converted = HiveMindHelper.Storage.ConvertFromStorageFormat(data, typeof(T), _options.Get(connection.Environment), _cache).CastTo<T>();
                _logger.Log($"Fetched data <{name}> from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return (true, converted);
            }
            return (false, default);
        }   
    }
}
