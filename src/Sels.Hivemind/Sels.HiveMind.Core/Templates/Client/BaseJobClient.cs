using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Client;
using Sels.Core;
using Sels.HiveMind.Query.Job;

namespace Sels.HiveMind.Templates.Client
{
    /// <summary>
    /// Base class for creating job clients.
    /// </summary>
    /// <typeparam name="TReadOnlyJob">The type used to return a readonly job</typeparam>
    /// <typeparam name="TLockedJob">The type used to return a locked job</typeparam>
    /// <typeparam name="TSortTarget">The type used to determine the sort order</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TStateStorageData">The type of storage data used to store the job states</typeparam>
    /// <typeparam name="TStorageData">The type of storage data used by the job</typeparam>
    public abstract class BaseJobClient<TService, TReadOnlyJob, TLockedJob, TSortTarget, TStorageData, TState, TStateStorageData> : BaseClient, IJobClient<TReadOnlyJob, TLockedJob, TSortTarget>
        where TReadOnlyJob : class, IReadOnlyJob
        where TLockedJob : class, TReadOnlyJob, IAsyncDisposable
        where TStorageData : JobStorageData
        where TService : IQueryJobService<TStorageData, TState, TStateStorageData, TSortTarget>
    {
        // Fields
        /// <summary>
        /// The service used to interact with the jobs.
        /// </summary>
        protected readonly TService _jobService;
        /// <summary>
        /// Used to fetch the options per environment.
        /// </summary>
        protected readonly IOptionsMonitor<HiveMindOptions> _options;
        /// <summary>
        /// Used to create service scope for the jobs.
        /// </summary>
        protected readonly IServiceProvider _serviceProvider;

        /// <inheritdoc cref="BaseJobClient{TService, TReadOnlyJob, TLockedJob, TStorageData, TState, TStateStorageData}"/>
        /// <param name="jobService"><inheritdoc cref="_jobService"/></param>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="loggerFactory"><inheritdoc cref="_loggerFactory"/></param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        public BaseJobClient(IServiceProvider serviceProvider, IOptionsMonitor<HiveMindOptions> options, TService jobService, IStorageProvider storageProvider, ILoggerFactory loggerFactory = null, ILogger logger = null) : base(storageProvider, loggerFactory, logger)
        {
            _jobService = jobService.ValidateArgument(nameof(jobService));
            _options = options.ValidateArgument(nameof(options));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
        }

        /// <inheritdoc/>
        public async Task<TReadOnlyJob> GetAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            var jobStorage = await _jobService.GetAsync(id, connection, token).ConfigureAwait(false);
            var job = CreateReadOnlyJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, false);

            _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            return job;
        }
        /// <inheritdoc/>
        public async Task<TReadOnlyJob> TryGetAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> if it exists", id, connection.Environment);
            var (_, data) = await _jobService.FetchAsync(id, connection, Guid.NewGuid().ToString(), false, token).ConfigureAwait(false);

            if (data != null)
            {
                var job = CreateReadOnlyJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, data, false);

                _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return job;
            }
            else
            {
                _logger.Log($"Job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> does not exist", id, connection.Environment);
            }

            return null;
        }
        /// <inheritdoc/>
        public async Task<TLockedJob> GetWithLockAsync(IStorageConnection connection, string id, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with write lock", id, connection.Environment);

            // Try lock first
            var (wasLocked, data) = await _jobService.FetchAsync(id, connection, requester, true, token).ConfigureAwait(false);
            if (data == null) throw new JobNotFoundException(id, connection.Environment);
            if(!wasLocked) throw new JobAlreadyLockedException(id, connection.Environment, requester, data.Lock.LockedBy);
            _logger.Debug($"Got lock on job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, requester);

            // Then fetch
            var job = CreateLockedJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, data);

            _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with write lock for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, requester);
            return job;
        }
        /// <inheritdoc/>
        public async Task<TReadOnlyJob> GetAndTryLockAsync(IStorageConnection connection, string id, string requester, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with optional write lock for <{requester}>", id, connection.Environment);

            // Try lock first
            var (wasLocked, data) = await _jobService.FetchAsync(id, connection, requester, true, token).ConfigureAwait(false);
            if (data == null) throw new JobNotFoundException(id, connection.Environment);

            _logger.Debug(wasLocked ? $"Got lock on job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>" : $"Could not get lock on job <{id}> from environment <{connection.Environment}> for <{requester}>", id, connection.Environment, requester);
            var job = CreateReadOnlyJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, data, wasLocked);

            _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with write lock for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, job?.Lock?.LockedBy);
            return job;
        }
        /// <inheritdoc/>
        public async Task<TReadOnlyJob> TryGetAndTryLockAsync(IStorageConnection connection, string id, string requester, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with optional write lock for <{requester}> if it exists", id, connection.Environment);

            // Try lock first
            var (wasLocked, data) = await _jobService.FetchAsync(id, connection, requester, true, token).ConfigureAwait(false);

            if (!wasLocked)
            {
                _logger.Log($"Job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> does not exist", id, connection.Environment);
                return null;
            }

            _logger.Debug(wasLocked ? $"Got lock on job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>" : $"Could not get lock on job <{id}> from environment <{connection.Environment}> for <{requester}>", id, connection.Environment, requester);

            // Then fetch
            var job = CreateReadOnlyJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, data, wasLocked);

            _logger.Log($"Fetched job <{HiveLog.Job.IdParam}> from environment <{HiveLog.EnvironmentParam}> with write lock for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, job?.Lock?.LockedBy);
            return job;
        }

        /// <inheritdoc/>
        public async Task<IClientQueryResult<TLockedJob>> SearchAndLockAsync(IStorageConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder, int limit = 100, string requester = null, bool allowAlreadyLocked = false, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            conditionBuilder.ValidateArgument(nameof(conditionBuilder));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Trying the lock the next <{limit}> jobs in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            var queryConditions = new JobQueryConditions(conditionBuilder);

            var lockedJobs = await _jobService.SearchAndLockAsync(connection, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<TLockedJob> jobs = new List<TLockedJob>();

            try
            {
                foreach (var result in lockedJobs)
                {
                    jobs.Add(CreateLockedJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result));
                }

                _logger.Log($"Dequeued <{lockedJobs.Length}> jobs from environment <{HiveLog.EnvironmentParam}>", connection.Environment);
                return CreateLockedQueryResult(connection.Environment, jobs, false);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                foreach (var job in jobs)
                {
                    try
                    {
                        await job.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }

                if (exceptions.HasValue()) throw new AggregateException(Helper.Collection.Enumerate(ex, exceptions));
                throw;
            }
        }
        /// <inheritdoc/>
        public async Task<IClientQueryResult<TReadOnlyJob>> SearchAsync(IStorageConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder = null, int pageSize = 1000, int page = 1, TSortTarget orderBy = default, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Querying jobs in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            var queryConditions = conditionBuilder != null ? new JobQueryConditions(conditionBuilder) : new JobQueryConditions();

            var results = await _jobService.SearchAsync(connection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<TReadOnlyJob> jobs = new List<TReadOnlyJob>();

            try
            {
                foreach (var result in results)
                {
                    jobs.Add(CreateReadOnlyJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, false));
                }

                _logger.Log($"Query returned <{results.Length}> jobs");
                return CreateReadOnlyQueryResult(connection.Environment, jobs);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                foreach (var job in jobs)
                {
                    try
                    {
                        await job.DisposeAsync();
                    }
                    catch (Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }

                if (exceptions.HasValue()) throw new AggregateException(Helper.Collection.Enumerate(ex, exceptions));
                throw;
            }
        }
        /// <inheritdoc/>
        public async Task<long> CountAsync(IStorageConnection connection, Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> conditionBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Querying jobs in environment <{HiveLog.EnvironmentParam}> to get the amount matching the query condition", connection.Environment);

            var queryConditions = conditionBuilder != null ? new JobQueryConditions(conditionBuilder) : new JobQueryConditions();

            var matching = await _jobService.CountAsync(connection, queryConditions, token).ConfigureAwait(false);

            _logger.Log($"There are <{matching}> jobs in environment <{HiveLog.EnvironmentParam}> that match the query condition", connection.Environment);
            return matching;
        }


        /// <inheritdoc/>
        public async Task<IClientQueryResult<TLockedJob>> GetTimedOutAsync(IStorageConnection connection, string requester, int limit = 100, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgument(nameof(requester));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Trying the fetch the next <{limit}> jobs in environment <{HiveLog.EnvironmentParam}> that timed out for <{requester}.", connection.Environment);

            var lockedJobs = await _jobService.GetTimedOutJobs(connection, limit, requester, token).ConfigureAwait(false);
            List<TLockedJob> jobs = new List<TLockedJob>();

            try
            {
                foreach (var result in lockedJobs)
                {
                    jobs.Add(CreateLockedJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result));
                }

                _logger.Log($"Dequeued <{lockedJobs.Length}> timed out jobs from environment <{HiveLog.EnvironmentParam}> for <{requester}>", connection.Environment);
                return CreateLockedQueryResult(connection.Environment, jobs, true);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                foreach (var job in jobs)
                {
                    try
                    {
                        await job.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }

                if (exceptions.HasValue()) throw new AggregateException(Helper.Collection.Enumerate(ex, exceptions));
                throw;
            }
        }
        /// <inheritdoc/>
        public async Task<string[]> GetAllQueuesAsync(IStorageConnection connection, string? prefix = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching all distinct queues being used by all jobs in environment <{HiveLog.EnvironmentParam}> that start with prefix <{prefix}>", connection.Environment);

            var queues = await GetDistinctQueues(connection, prefix, token).ConfigureAwait(false);

            _logger.Log($"Fetched all {(queues?.Length ?? 0)} queues being used by all jobs in environment <{HiveLog.EnvironmentParam}> that start with prefix <{prefix}>", connection.Environment);

            return queues ?? Array.Empty<string>();
        }

        /// <summary>
        /// Creates a readonly job.
        /// </summary>
        /// <param name="serviceScope">The service scope opened for the new job</param>
        /// <param name="options">The options configured for the job</param>
        /// <param name="environment">The environment the job is from</param>
        /// <param name="storageData">The state of the job in storage format</param>
        /// <param name="hasLock">True if the new instance has the active lock on the job, otherwise false</param>
        /// <returns>A new readonly job created from <paramref name="storageData"/></returns>
        protected abstract TReadOnlyJob CreateReadOnlyJob(AsyncServiceScope serviceScope, HiveMindOptions options, string environment, TStorageData storageData, bool hasLock);
        /// <summary>
        /// Creates a locked job.
        /// </summary>
        /// <param name="serviceScope">The service scope opened for the new job</param>
        /// <param name="options">The options configured for the job</param>
        /// <param name="environment">The environment the job is from</param>
        /// <param name="storageData">The state of the job in storage format</param>
        /// <returns>A new readonly job created from <paramref name="storageData"/></returns>
        protected abstract TLockedJob CreateLockedJob(AsyncServiceScope serviceScope, HiveMindOptions options, string environment, TStorageData storageData);
        /// <summary>
        /// Creates a query result from the provided data.
        /// </summary>
        /// <param name="environment">The environment <paramref name="jobs"/> are from</param>
        /// <param name="jobs">The jobs to create the result for</param>
        /// <param name="total">The total amount of jobs amtching the original search conditions</param>
        /// <returns>A query result created for <paramref name="jobs"/></returns>
        protected abstract IClientQueryResult<TLockedJob> CreateReadOnlyQueryResult(string environment, IReadOnlyList<TReadOnlyJob> jobs);
        /// <summary>
        /// Creates a query result from the provided data.
        /// </summary>
        /// <param name="environment">The environment <paramref name="jobs"/> are from</param>
        /// <param name="jobs">The jobs to create the result for</param>
        /// <param name="total">The total amount of jobs amtching the original search conditions</param>
        /// <param name="isTimedOut">True if <paramref name="jobs"/> are timed out jobs</param>
        /// <returns>A query result created for <paramref name="jobs"/></returns>
        protected abstract IClientQueryResult<TLockedJob> CreateLockedQueryResult(string environment, IReadOnlyList<TLockedJob> jobs, bool isTimedOut);
        /// <summary>
        /// Tries to get the job data from storage if it exists.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="connection">The connection to use</param>
        /// <param name="token">Token that will be cancelled when the action is requested to stop</param>
        /// <returns>The job data from storage if it exists, otherwise false</returns>
        protected abstract Task<TStorageData> TryGetJobDataAsync(string id, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Retrieves all queues being used by all jobs.
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="token">Token that will be cancelled when the action is requested to stop</param>
        /// <returns>All queus being used by the current jobs</returns>
        protected abstract Task<string[]> GetDistinctQueues(IStorageConnection connection, string? prefix = null, CancellationToken token = default);
    }
}
