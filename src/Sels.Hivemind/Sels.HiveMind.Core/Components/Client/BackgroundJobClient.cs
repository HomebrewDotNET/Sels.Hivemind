using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Templates.Client;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using Sels.Core.Extensions.Reflection;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Requests;
using Sels.HiveMind.Query.Job;
using System.Linq;
using Sels.Core;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.DateTimes;
using static Sels.HiveMind.HiveMindConstants;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Text;
using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage.Job;
using static Sels.HiveMind.HiveLog;
using BackgroundJob = Sels.HiveMind.Job.BackgroundJob;

namespace Sels.HiveMind.Client
{
    /// <inheritdoc cref="IBackgroundJobClient"/>
    public class BackgroundJobClient : BaseJobClient<IBackgroundJobService, IReadOnlyBackgroundJob, ILockedBackgroundJob, BackgroundJobStorageData, IBackgroundJobState, JobStateStorageData>, IBackgroundJobClient
    {
        // Fields
        private readonly IMemoryCache _cache;

        /// <inheritdoc cref="BackgroundJobClient"/>
        /// <param name="service">Service used to manage job state</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="cache">Optional memory cache that cam be used to speed up conversions</param>
        /// <param name="loggerFactory"><inheritdoc cref="BaseClient._loggerFactory"/></param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public BackgroundJobClient(IBackgroundJobService service, IServiceProvider serviceProvider, IOptionsMonitor<HiveMindOptions> options, IStorageProvider storageProvider, IMemoryCache cache = null, ILoggerFactory loggerFactory = null, ILogger<BackgroundJobClient> logger = null) : base(serviceProvider, options, service, storageProvider, loggerFactory, logger)
        {
            _cache = cache;
        }

        /// <inheritdoc/>
        public Task<string> CreateAsync<T>(IStorageConnection connection, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            typeof(T).ValidateArgumentInstanceable(nameof(T));

            _logger.Log($"Creating new background job of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.Environment}>", connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<string> CreateAsync(IStorageConnection connection, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));

            _logger.Log($"Creating new static background job in environment <{HiveLog.Environment}>", connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, invocationInfo, jobBuilder, token);
        }
        public Task<string> CreateAsync<T>(IStorageConnection connection, Expression<Action<T>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            typeof(T).ValidateArgumentInstanceable(nameof(T));

            _logger.Log($"Creating new background job of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.Environment}>", connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, invocationInfo, jobBuilder, token);
        }

        public Task<string> CreateAsync(IStorageConnection connection, Expression<Action> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));

            _logger.Log($"Creating new static background job in environment <{HiveLog.Environment}>", connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, invocationInfo, jobBuilder, token);
        }

        /// <inheritdoc/>
        public async Task<IClientQueryResult<ILockedBackgroundJob>> SearchAndLockAsync(IStorageConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder, int limit = 100, string requester = null, bool allowAlreadyLocked = false, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            conditionBuilder.ValidateArgument(nameof(conditionBuilder));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Trying the lock the next <{limit}> background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            var queryConditions = new BackgroundJobQueryConditions(conditionBuilder);

            var (lockedJobs, total) = await _jobService.SearchAndLockAsync(connection, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach (var result in lockedJobs)
                {
                    backgroundJobs.Add(new BackgroundJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, true));
                }

                _logger.Log($"Dequeued <{lockedJobs.Length}> background jobs of the total <{total}> jobs matching the query condition from environment <{HiveLog.Environment}>", connection.Environment);
                return new QueryResult<BackgroundJob>(this, connection.Environment, backgroundJobs, total, false);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                foreach (var job in backgroundJobs)
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
        public async Task<IClientQueryResult<IReadOnlyBackgroundJob>> SearchAsync(IStorageConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, int pageSize = 1000, int page = 1, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Querying background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            var queryConditions = conditionBuilder != null ? new BackgroundJobQueryConditions(conditionBuilder) : new BackgroundJobQueryConditions();

            var (results, total) = await _jobService.SearchAsync(connection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach(var result in results)
                {
                    backgroundJobs.Add(new BackgroundJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, false));
                }

                _logger.Log($"Query returned <{results.Length}> background jobs of the total <{total}> jobs matching the query condition");
                return new QueryResult<BackgroundJob>(this, connection.Environment, backgroundJobs, total, false);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                foreach(var job in backgroundJobs)
                {
                    try
                    {
                        await job.DisposeAsync();
                    }
                    catch(Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }

                if (exceptions.HasValue()) throw new AggregateException(Helper.Collection.Enumerate(ex, exceptions));
                throw;
            }
        }
        /// <inheritdoc/>
        public async Task<long> CountAsync(IStorageConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Querying background jobs in environment <{HiveLog.Environment}> to get the amount matching the query condition", connection.Environment);

            var queryConditions = conditionBuilder != null ? new BackgroundJobQueryConditions(conditionBuilder) : new BackgroundJobQueryConditions();

            var matching = await _jobService.CountAsync(connection, queryConditions, token).ConfigureAwait(false);

            _logger.Log($"There are <{matching}> background jobs in environment <{HiveLog.Environment}> that match the query condition", connection.Environment);
            return matching;
        }

        private async Task<string> CreateAsync(IStorageConnection connection, InvocationInfo invocationInfo, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            invocationInfo.ValidateArgument(nameof(invocationInfo));

            var options = _options.Get(connection.Environment);
            var builder = new JobBuilder(this, connection, options, _cache, jobBuilder);
            var job = new BackgroundJob(_serviceProvider.CreateAsyncScope(), options, connection.Environment, builder.Queue, builder.Priority, invocationInfo, builder.Properties, builder.Middleware);

            try
            {

                IBackgroundJobState state = builder.ElectionState;
                // Move to enqueued state by default if no custom state is set
                if (state == null) state = new EnqueuedState() { Reason = "Enqueued by default during creation"};

                // Transition to initial state
                _logger.Debug($"Triggering state election for new job to transition into state <{HiveLog.Job.State}>", state.Name);
                await job.ChangeStateAsync(connection, state, token).ConfigureAwait(false);

                // Save changes
                await job.SaveChangesAsync(connection, false, token).ConfigureAwait(false);
                _logger.Log($"Created job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, connection.Environment);

                return job.Id;
            }
            finally
            {
                // Dispose job after connection closes
                if (connection.HasTransaction) connection.OnCommitted(async t => await job.DisposeAsync().ConfigureAwait(false));
                else await job.DisposeAsync().ConfigureAwait(false);
            }
        }


        /// <inheritdoc/>
        protected override IReadOnlyBackgroundJob CreateReadOnlyJob(AsyncServiceScope serviceScope, HiveMindOptions options, string environment, BackgroundJobStorageData storageData, bool hasLock)
        {
            return new BackgroundJob(serviceScope, options, environment, storageData, hasLock);
        }
        /// <inheritdoc/>
        protected override ILockedBackgroundJob CreateLockedJob(AsyncServiceScope serviceScope, HiveMindOptions options, string environment, BackgroundJobStorageData storageData)
        {
            return new BackgroundJob(serviceScope, options, environment, storageData, true);
        }
        protected override IClientQueryResult<ILockedBackgroundJob> CreateQueryResult(string environment, IReadOnlyList<ILockedBackgroundJob> jobs, long total, bool isTimedOut)
        {
            return new QueryResult<BackgroundJob>(this, environment, jobs.OfType<BackgroundJob>(), total, isTimedOut);
        }
        /// <inheritdoc/>
        protected override Task<BackgroundJobStorageData> TryGetJobDataAsync(string id, IStorageConnection connection, CancellationToken token = default)
        => connection.Storage.GetBackgroundJobAsync(id, connection, token);
        /// <inheritdoc/>
        protected override Task<string[]> GetDistinctQueues(IStorageConnection connection, CancellationToken token = default)
        => connection.Storage.GetAllBackgroundJobQueuesAsync(connection, token);

        #region Classes
        private class JobBuilder : BaseJobBuilder<IBackgroundJobBuilder>, IBackgroundJobBuilder
        {
            // Properties
            /// <inheritdoc/>
            public IBackgroundJobClient Client { get; }
            public IBackgroundJobState ElectionState { get; private set; }

            protected override IBackgroundJobBuilder Builder => this;

            public JobBuilder(IBackgroundJobClient client, IStorageConnection connection, HiveMindOptions options, IMemoryCache cache, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> configurator) : base(connection, options, cache)
            {
                Client = client.ValidateArgument(nameof(client));

                if (configurator != null) configurator(this);
            }

            /// <inheritdoc/>
            public IBackgroundJobBuilder InState(IBackgroundJobState state)
            {
                ElectionState = state.ValidateArgument(nameof(state));
                return this;
            }
            /// <inheritdoc/>
            protected override void CheckMiddleware(Type type, object context)
            {
                type.ValidateArgumentAssignableTo(nameof(type), typeof(IBackgroundJobMiddleware));
            }
        }

        private class QueryResult<T> : IClientQueryResult<T> where T : BackgroundJob
        {
            // Fields
            private readonly IBackgroundJobClient _client;
            private readonly string _environment;
            private readonly bool _isTimedOut;

            // Properties
            /// <inheritdoc/>
            public long Total { get; }
            /// <inheritdoc/>
            public IReadOnlyList<T> Results { get; }

            public QueryResult(IBackgroundJobClient client, string environment, IEnumerable<T> results, long total, bool isTimedOut)
            {
                _client = client.ValidateArgument(nameof(client));
                _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
                Results = results.ValidateArgument(nameof(results)).ToArray();
                Total = total;
                _isTimedOut = isTimedOut;

            }
            /// <inheritdoc/>
            public async ValueTask DisposeAsync()
            {
                var exceptions = new List<Exception>();

                // Try and release locks in bulk
                var locked = Results.Where(x => x.HasLock).GroupAsDictionary(x => x.Lock.LockedBy, x => x.Id);
                if (locked.HasValue())
                {
                    // Don't unlock when dealing with timed out background jobs as they need to remain locked to time out.
                    if (!_isTimedOut)
                    {
                        await using (var connection = await _client.OpenConnectionAsync(_environment, true).ConfigureAwait(false))
                        {
                            foreach (var (holder, ids) in locked)
                            {
                                await connection.StorageConnection.Storage.UnlockBackgroundsJobAsync(ids.ToArray(), holder, connection.StorageConnection).ConfigureAwait(false);
                            }

                            await connection.CommitAsync().ConfigureAwait(false);
                        }
                    }
                    Results.Where(x => x.HasLock).ForceExecute(x => x.RemoveHoldOnLock());
                }

                // Dispose jobs
                foreach (var result in Results)
                {
                    try
                    {
                        if (result is IAsyncDisposable asyncDisposable)
                        {
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        }
                        else if (result is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.HasValue()) throw new AggregateException(exceptions);
            }
        }
        #endregion
    }
}
