﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;
using static Sels.HiveMind.HiveMindConstants;
using Expression = System.Linq.Expressions.Expression;
using Sels.Core.Conversion.Converters;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Parameters;
using Sels.Core.Extensions.Linq;
using System.Data.Common;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Templates.Service;
using Sels.HiveMind.Storage.Job.Background;
using Sels.HiveMind.Job.Background;
using System.ComponentModel.DataAnnotations;

namespace Sels.HiveMind.Service
{
    /// <inheritdoc cref="IBackgroundJobService"/>
    public class BackgroundJobService : BaseJobService, IBackgroundJobService
    {
        // Fields
        private readonly BackgroundJobValidationProfile _backgroundJobValidationProfile;
        private readonly QueryValidationProfile _backgroundJobQueryValidationProfile;

        /// <inheritdoc cref="BackgroundJobService"/>
        /// <param name="options"><inheritdoc cref="BaseJobService._options"/></param>
        /// <param name="cache"><inheritdoc cref="BaseJobService._options"/></param>
        /// <param name="backgroundJobValidationProfile">Used to validate background jobs</param>
        /// <param name="backgroundJobQueryValidationProfile">Used to validate query parameters</param>
        /// <param name="logger"><inheritdoc cref="BaseJobService._logger"/></param>
        public BackgroundJobService(IOptionsMonitor<HiveMindOptions> options, IMemoryCache cache, BackgroundJobValidationProfile backgroundJobValidationProfile, QueryValidationProfile backgroundJobQueryValidationProfile, ILogger<BackgroundJobService>? logger = null) : base(options, cache, logger)
        {
            _backgroundJobValidationProfile = backgroundJobValidationProfile.ValidateArgument(nameof(backgroundJobValidationProfile));
            _backgroundJobQueryValidationProfile = backgroundJobQueryValidationProfile.ValidateArgument(nameof(backgroundJobQueryValidationProfile));
        }

        /// <inheritdoc/>
        public async Task<string> StoreAsync(IStorageConnection connection, BackgroundJobStorageData job, bool releaseLock, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            job.ValidateArgument(nameof(job));

            // Validate storage data
            var result = await _backgroundJobValidationProfile.ValidateAsync(job, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(job);

            return await RunTransaction(connection, async () =>
            {
                // Update
                if (job.Id.HasValue())
                {
                    _logger.Log($"Updating background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", job.Id, connection.Environment);
                    ValidateLock(job, connection.Environment);
                    var wasUpdated = await connection.Storage.TryUpdateBackgroundJobAsync(job, connection, releaseLock, token).ConfigureAwait(false);
                    if (!wasUpdated) throw new JobLockStaleException(job.Id, connection.Environment);
                    _logger.Log($"Updated background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", job.Id, connection.Environment);
                    return job.Id;
                }
                // Create
                else
                {
                    _logger.Log($"Creating new background job in environment <{HiveLog.EnvironmentParam}>");
                    var id = await connection.Storage.CreateBackgroundJobAsync(job, connection, token).ConfigureAwait(false);
                    _logger.Log($"Created background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                    return id;
                }
            }, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Setting lock heartbeat on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{holder}>", id, connection.Environment);

            var (wasLocked, jobLock) = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryHeartbeatLockOnBackgroundJobAsync(id, holder, connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Background job <{HiveLog.Job.IdParam}> does not exist in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                throw new JobNotFoundException(id, connection.Environment);
            }

            if (wasLocked)
            {
                _logger.Log($"Lock heartbeat on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> has been set to <{jobLock.LockHeartbeatUtc}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
                return jobLock;
            }
            else
            {
                _logger.Warning($"Lock heartbeat on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> could not be set for <{holder}> because it is already locked by <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, jobLock.LockedBy);
                throw new JobAlreadyLockedException(id, connection.Environment, holder, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task<BackgroundJobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            var (_, job) = await FetchAsync(id, connection, Guid.NewGuid().ToString(), false, token).ConfigureAwait(false);
            if (job == null) throw new JobNotFoundException(id, connection.Environment);
            return job;
        }
        /// <inheritdoc/>
        public async Task<(bool WasLocked, BackgroundJobStorageData Data)> FetchAsync(string id, IStorageConnection connection, string requester, bool tryLock, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            bool wasLocked = false;
            BackgroundJobStorageData job = null;

            if (tryLock)
            {
                _logger.Debug($"Trying to fetch backgroundjob job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with a lock for <{requester}>", id, connection.Environment);
                (wasLocked, job) = await RunTransaction(connection, () => connection.Storage.TryLockAndTryGetBackgroundJobAsync(id, requester, connection, token), token).ConfigureAwait(false);
            }
            else
            {
                _logger.Debug($"Lock is not required on backgroundjob job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>. Doing normal fetch", id, connection.Environment);
                job = await connection.Storage.GetBackgroundJobAsync(id, connection, token).ConfigureAwait(false);
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
        public async Task<BackgroundJobStorageData[]> SearchAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Searching for background jobs in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await connection.Storage.SearchBackgroundJobsAsync(connection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            _logger.Log($"Search for background jobs in environment <{HiveLog.EnvironmentParam}> returned <{result.Length}> jobs", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task<long> CountAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));

            _logger.Log($"Searching for an amount of background jobs in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.CountBackgroundJobsAsync(connection, queryConditions, token), token).ConfigureAwait(false);

            _logger.Log($"Search for an amount of background jobs in environment <{HiveLog.EnvironmentParam}> returned <{result}> matching", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task<BackgroundJobStorageData[]> SearchAndLockAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to lock the next <{limit}> background jobs in environment <{HiveLog.EnvironmentParam}> for <{requester}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.LockBackgroundJobsAsync(connection, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token), token).ConfigureAwait(false);

            _logger.Log($"<{result.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> are now locked by <{HiveLog.Job.LockHolderParam}>", connection.Environment, requester);
            return result;
        }
        /// <inheritdoc/>
        public async Task<(bool Exists, T? Data)> TryGetDataAsync<T>(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgument(nameof(name));

            _logger.Log($"Trying to fetch data <{name}> from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            if (await connection.Storage.TryGetBackgroundJobDataAsync(connection, id, name, token).ConfigureAwait(false) is (true, var data))
            {
                _logger.Debug($"Fetched data <{name}> from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Converting to <{typeof(T)}>", id, connection.Environment);
                var converted = HiveMindHelper.Storage.ConvertFromStorageFormat(data, typeof(T), _options.Get(connection.Environment), _cache).CastTo<T>();
                _logger.Log($"Fetched data <{name}> from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return (true, converted);
            }
            return (false, default);
        }
        /// <inheritdoc/>
        public async Task SetDataAsync<T>(IStorageConnection connection, string id, string name, T value, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            _logger.Log($"Saving data <{name}> to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            _logger.Debug($"Converting data <{name}> of type <{value.GetType()}> for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for storage", id, connection.Environment);

            var converted = HiveMindHelper.Storage.ConvertToStorageFormat(value, _options.Get(connection.Environment), _cache)!;

            await RunTransaction(connection, () => connection.Storage.SetBackgroundJobDataAsync(connection, id, name, converted, token), token).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
        }
        /// <inheritdoc/>
        public async Task<BackgroundJobStorageData[]> GetTimedOutJobs(IStorageConnection connection, int limit, string requester, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgument(nameof(requester));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Checking if there are timed out background jobs in environment <{HiveLog.EnvironmentParam}> which will be locked for <{requester}>", connection.Environment);

            var options = _options.Get(connection.Environment);

            var jobs = await RunTransaction(connection, () => connection.Storage.GetTimedOutBackgroundJobs(connection, limit, requester, options.LockTimeout, token), token).ConfigureAwait(false);

            if (jobs.HasValue())
            {
                _logger.Log($"Got <{jobs.Length}> timed out background jobs in environment <{HiveLog.EnvironmentParam}> to lock for <{requester}>", connection.Environment);
                return jobs;
            }
            else
            {
                _logger.Log($"No timed out background jobs in environment <{HiveLog.EnvironmentParam}> to lock for <{requester}>", connection.Environment);
                return Array.Empty<BackgroundJobStorageData>();
            }
        }
        /// <inheritdoc/>
        public async Task<string[]> DeleteBackgroundJobsAsync(IStorageConnection connection, int amount, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            connection = Guard.IsNotNull(connection);
            amount = Guard.IsLarger(amount, 0);
            queryConditions = Guard.IsNotNull(queryConditions);

            _logger.Log($"Deleting at most <{amount}> background jobs in environment <{HiveLog.EnvironmentParam}> matching conditions <{queryConditions}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Delete jobs
            var result = await RunTransaction(connection, () => connection.Storage.DeleteBackgroundJobsAsync(connection, amount, queryConditions, token), token).ConfigureAwait(false);

            _logger.Log($"<{result.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> deleted from storage", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task CreateActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            _logger.Log($"Creating action of type <{action.Type}> for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);

            // Validate
            var result = await _backgroundJobValidationProfile.ValidateAsync(action, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(action);

            // Execute
            await RunTransaction(connection, () => connection.Storage.CreateBackgroundJobActionAsync(connection, action, token), token).ConfigureAwait(false);

            _logger.Log($"Created action <{action.Id}> of type <{action.Type}> for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);
        }
        /// <inheritdoc/>
        public async Task<ActionInfo[]> GetNextActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Fetching the next <{limit}> actions for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if they exists", id, connection.Environment);

            var actions = await RunTransaction(connection, () => connection.Storage.GetNextBackgroundJobActionsAsync(connection, id, limit, token), token).ConfigureAwait(false) ?? Array.Empty<ActionInfo>();

            _logger.Log($"Fetched <{actions.Length}> actions for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            return actions;
        }
        /// <inheritdoc/>
        public async Task<bool> DeleteActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));

            _logger.Log($"Removing action <{id}> in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            var wasDeleted = await RunTransaction(connection, () => connection.Storage.DeleteBackgroundJobActionByIdAsync(connection, id, token), token).ConfigureAwait(false);

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
        public virtual IBackgroundJobState ConvertToState(JobStateStorageData stateData, string environment)
        {
            stateData.ValidateArgument(nameof(stateData));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            _logger.Log($"Converting state storage formate into a new instance of <{stateData.OriginalTypeName}>");

            var constructor = GetStateConverter<IBackgroundJobState>(stateData, environment);

            var options = _options.Get(environment);

            var properties = new LazyPropertyInfoDictionary(options, _cache);
            //if (stateData.Properties.HasValue()) stateData.Properties.Execute(x => properties.Add(x.Name, new LazyPropertyInfo(x, options, _cache)));
            properties.Add(nameof(IBackgroundJobState.ElectedDateUtc), new LazyPropertyInfo(stateData.ElectedDateUtc, options, _cache));
            properties.Add(nameof(IBackgroundJobState.Reason), new LazyPropertyInfo(stateData.Reason, options, _cache));
            properties.Add(nameof(IBackgroundJobState.Name), new LazyPropertyInfo(stateData.Name, options, _cache));

            return constructor(properties);
        }
        /// <inheritdoc/>
        public virtual IEnumerable<StorageProperty> GetStorageProperties(IBackgroundJobState state, string environment)
        {
            state.ValidateArgument(nameof(state));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            _logger.Log($"Fetching all public properties from <{state}>");

            var options = _options.Get(environment);
            var getter = GetPropertyFetcher<IBackgroundJobState>(state, environment);

            var dictionary = new Dictionary<string, object>();
            getter(state, dictionary);
            dictionary.Remove(nameof(IBackgroundJobState.ElectedDateUtc));
            dictionary.Remove(nameof(IBackgroundJobState.Reason));
            dictionary.Remove(nameof(IBackgroundJobState.Name));

            return dictionary.Where(x => x.Value != null).Select(x => new StorageProperty(x.Key, x.Value, options, _cache));
        }
    }
}
