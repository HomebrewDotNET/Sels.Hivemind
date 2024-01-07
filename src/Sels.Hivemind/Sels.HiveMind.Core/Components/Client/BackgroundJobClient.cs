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
using Sels.HiveMind;
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

namespace Sels.HiveMind.Client
{
    /// <inheritdoc cref="IBackgroundJobClient"/>
    public class BackgroundJobClient : BaseClient, IBackgroundJobClient
    {
        // Fields
        private readonly IBackgroundJobService _service;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<HiveMindOptions> _options;
        private readonly IMemoryCache _cache;

        /// <inheritdoc cref="BackgroundJobClient"/>
        /// <param name="service">Service used to manager job state</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="cache">Optional memory cache that cam be used to speed up conversions</param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public BackgroundJobClient(IBackgroundJobService service, IServiceProvider serviceProvider, IOptionsMonitor<HiveMindOptions> options, IStorageProvider storageProvider, IMemoryCache cache = null, ILogger<BackgroundJobClient> logger = null) : base(storageProvider, logger)
        {
            _service = service.ValidateArgument(nameof(service));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }

        /// <inheritdoc/>
        public Task<string> CreateAsync<T>(IClientConnection connection, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            typeof(T).ValidateArgumentInstanceable(nameof(T));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Creating new background job of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.Environment}>", connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(clientConnection, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<string> CreateAsync(IClientConnection connection, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Creating new static background job in environment <{HiveLog.Environment}>", connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(clientConnection, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyBackgroundJob> GetAsync(IClientConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Fetching background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);

            var jobStorage = await _service.GetAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);
            var job = new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, false);

            _logger.Log($"Fetched background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);
            return job;
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyBackgroundJob> TryGetAsync(IClientConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Fetching background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> if it exists", id, connection.Environment);

            var jobStorage = await connection.StorageConnection.Storage.GetBackgroundJobAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);

            if(jobStorage != null)
            {
                var job = new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, false);

                _logger.Log($"Fetched background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);
                return job;
            }
            else
            {
                _logger.Log($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> does not exist", id, connection.Environment);
            }
            
            return null;
        }
        /// <inheritdoc/>
        public async Task<ILockedBackgroundJob> GetWithLockAsync(IClientConnection connection, string id, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Fetching background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> with write lock", id, connection.Environment);

            // Try lock first
            var jobLock = await _service.LockAsync(id, clientConnection.StorageConnection, requester, token).ConfigureAwait(false);

            _logger.Debug($"Got lock on background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);

            // Then fetch
            var jobStorage = await _service.GetAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);
            var job = new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, true);

            _logger.Log($"Fetched background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> with write lock for <{HiveLog.Job.LockHolder}>", id, connection.Environment, job?.Lock?.LockedBy);
            return job;
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyBackgroundJob> GetAndTryLockAsync(IClientConnection connection, string id, string requester, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Fetching background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> with optional write lock for <{requester}>", id, connection.Environment);

            // Try lock first
            var wasLocked = await _service.TryLockAsync(id, clientConnection.StorageConnection, requester, token).ConfigureAwait(false);

            _logger.Debug(wasLocked ? $"Got lock on background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>" : $"Could not get lock on background job <{id}> from environment <{connection.Environment}> for <{requester}>", id, connection.Environment, requester);

            // Then fetch
            var jobStorage = await _service.GetAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);
            var job = new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, wasLocked && requester.EqualsNoCase(jobStorage?.Lock?.LockedBy));

            _logger.Log($"Fetched background job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}> with write lock for <{HiveLog.Job.LockHolder}>", id, connection.Environment, job?.Lock?.LockedBy);
            return job;
        }
        /// <inheritdoc/>
        public async Task<IClientQueryResult<ILockedBackgroundJob>> DequeueAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder, int limit = 100, string requester = null, bool allowAlreadyLocked = false, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            conditionBuilder.ValidateArgument(nameof(conditionBuilder));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Trying the lock the next <{limit}> background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            var queryConditions = new BackgroundJobQueryConditions(conditionBuilder);

            var (lockedJobs, total) = await _service.LockAsync(connection.StorageConnection, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach (var result in lockedJobs)
                {
                    backgroundJobs.Add(new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, true));
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
        public async Task<IClientQueryResult<IReadOnlyBackgroundJob>> QueryAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, int pageSize = 1000, int page = 1, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Querying background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            var queryConditions = conditionBuilder != null ? new BackgroundJobQueryConditions(conditionBuilder) : new BackgroundJobQueryConditions();

            var (results, total) = await _service.SearchAsync(connection.StorageConnection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach(var result in results)
                {
                    backgroundJobs.Add(new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, false));
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
        public async Task<long> QueryCountAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Querying background jobs in environment <{HiveLog.Environment}> to get the amount matching the query condition", connection.Environment);

            var queryConditions = conditionBuilder != null ? new BackgroundJobQueryConditions(conditionBuilder) : new BackgroundJobQueryConditions();

            var matching = await _service.CountAsync(connection.StorageConnection, queryConditions, token).ConfigureAwait(false);

            _logger.Log($"There are <{matching}> background jobs in environment <{HiveLog.Environment}> that match the query condition", connection.Environment);
            return matching;
        }

        private async Task<string> CreateAsync(ClientStorageConnection clientConnection, InvocationInfo invocationInfo, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            clientConnection.ValidateArgument(nameof(clientConnection));
            invocationInfo.ValidateArgument(nameof(invocationInfo));

            var options = _options.Get(clientConnection.Environment);
            var builder = new JobBuilder(this, clientConnection, options, _cache, jobBuilder);
            var job = new BackgroundJob(_serviceProvider.CreateAsyncScope(), options, clientConnection.Environment, builder.Queue, builder.Priority, invocationInfo, builder.Properties, builder.Middleware);

            try
            {

                IBackgroundJobState state = builder.ElectionState;
                // Move to enqueued state by default if no custom state is set
                if (state == null) state = new EnqueuedState() { Reason = "Enqueued by default during creation"};

                // Transition to initial state
                _logger.Debug($"Triggering state election for new job to transition into state <{HiveLog.BackgroundJob.State}>", state.Name);
                await job.ChangeStateAsync(state, token).ConfigureAwait(false);

                // Save changes
                await job.SaveChangesAsync(clientConnection, false, token).ConfigureAwait(false);
                _logger.Log($"Created job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, clientConnection.Environment);

                return job.Id;
            }
            finally
            {
                // Dispose job after connection closes
                if (clientConnection.HasTransaction) clientConnection.StorageConnection.OnCommitted(async t => await job.DisposeAsync().ConfigureAwait(false));
                else await job.DisposeAsync().ConfigureAwait(false);
            }

        }
        /// <inheritdoc/>
        public async Task<IClientQueryResult<ILockedBackgroundJob>> GetTimedOutAsync(IClientConnection connection, string requester, int limit = 100, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgument(nameof(requester));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Trying the fetch the next <{limit}> background jobs in environment <{HiveLog.Environment}> that timed out for <{requester}.", connection.Environment);

            var lockedJobs = await _service.GetTimedOutBackgroundJobs(connection.StorageConnection, limit, requester, token).ConfigureAwait(false);
            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach (var result in lockedJobs)
                {
                    backgroundJobs.Add(new BackgroundJob(connection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, true));
                }

                _logger.Log($"Dequeued <{lockedJobs.Length}> timed out background jobs from environment <{HiveLog.Environment}> for <{requester}>", connection.Environment);
                return new QueryResult<BackgroundJob>(this, connection.Environment, backgroundJobs, lockedJobs.LongLength, true);
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
        public async Task<string[]> GetAllQueuesAsync(IClientConnection connection, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching all distinct queues being used by all background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            var queues = await connection.StorageConnection.Storage.GetAllBackgroundJobQueuesAsync(connection.StorageConnection, token).ConfigureAwait(false);

            _logger.Log($"Fetched all {(queues?.Length ?? 0)} queues being used by all background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            return queues ?? Array.Empty<string>();
        }

        #region Classes
        private class JobBuilder : IBackgroundJobBuilder
        {
            // Fields
            private readonly List<MiddlewareInfo> _middleware = new List<MiddlewareInfo>();
            private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();
            private readonly IMemoryCache _cache;
            private readonly HiveMindOptions _options;

            // Properties
            /// <inheritdoc/>
            public IBackgroundJobClient Client { get; }
            /// <inheritdoc/>
            public IClientConnection Connection { get; }
            public string Queue { get; private set; }
            public QueuePriority Priority { get; private set; }
            public IBackgroundJobState ElectionState { get; private set; }
            public IReadOnlyList<MiddlewareInfo> Middleware => _middleware;
            public IReadOnlyDictionary<string, object> Properties => _properties;

            public JobBuilder(IBackgroundJobClient client, IClientConnection connection, HiveMindOptions options, IMemoryCache cache, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> configurator)
            {
                Client = client.ValidateArgument(nameof(client));
                Connection = connection.ValidateArgument(nameof(connection));
                _cache = cache.ValidateArgument(nameof(cache));
                _options = options.ValidateArgument(nameof(options));

                InQueue(HiveMindConstants.Queue.DefaultQueue, QueuePriority.Normal);
                if (configurator != null) configurator(this);
            }

            /// <inheritdoc/>
            public IBackgroundJobBuilder InQueue(string queue, QueuePriority priority = QueuePriority.Normal)
            {
                Queue = queue.ValidateArgument(nameof(queue));
                Priority = priority;
                return this;
            }

            /// <inheritdoc/>
            public IBackgroundJobBuilder InState(IBackgroundJobState state)
            {
                ElectionState = state.ValidateArgument(nameof(state));
                return this;
            }

            /// <inheritdoc/>
            public IBackgroundJobBuilder WithProperty(string name, object value)
            {
                name.ValidateArgument(nameof(name));
                value.ValidateArgument(nameof(value));

                _properties.AddOrUpdate(name, value);
                return this;
            }

            /// <inheritdoc/>
            public IBackgroundJobBuilder WithMiddleWare<T>(object context, uint? priority) where T : class, IBackgroundJobMiddleware
            {
                typeof(T).ValidateArgumentInstanceable(nameof(T));
                _middleware.Add(new MiddlewareInfo(typeof(T), context, priority, _options, _cache));
                return this;
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
