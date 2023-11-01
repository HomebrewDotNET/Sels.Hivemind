using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Middleware.Job;
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
using Sels.HiveMind.Service.Job;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Sels.HiveMind.Query.Job;
using System.Linq;
using Sels.Core;
using Sels.Core.Extensions.Linq;

namespace Sels.HiveMind.Client
{
    /// <inheritdoc cref="IBackgroundJobClient"/>
    public class BackgroundJobClient : BaseClient, IBackgroundJobClient
    {
        // Fields
        private readonly IBackgroundJobService _service;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsSnapshot<HiveMindOptions> _options;

        /// <inheritdoc cref="BackgroundJobClient"/>
        /// <param name="service">Service used to manager job state</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public BackgroundJobClient(IBackgroundJobService service, IServiceProvider serviceProvider, IOptionsSnapshot<HiveMindOptions> options, IStorageProvider storageProvider, ILogger<BackgroundJobClient> logger = null) : base(storageProvider, logger)
        {
            _service = service.ValidateArgument(nameof(service));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _options = options.ValidateArgument(nameof(options));
        }

        /// <inheritdoc/>
        public Task<string> CreateAsync<T>(IClientConnection connection, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            typeof(T).ValidateArgumentInstanceable(nameof(T));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Creating new background job of type <{typeof(T).GetDisplayName()}> in environment <{connection.Environment}>");
            var invocationInfo = new InvocationInfo<T>(methodSelector);

            return CreateAsync(clientConnection, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<string> CreateAsync(IClientConnection connection, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Creating new static background job in environment <{connection.Environment}>");
            var invocationInfo = new InvocationInfo(methodSelector);

            return CreateAsync(clientConnection, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyBackgroundJob> GetAsync(IClientConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Fetching background job <{id}> from environment <{connection.Environment}>");

            var jobStorage = await _service.GetAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);
            var job = new BackgroundJob(connection.StorageConnection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, false);

            _logger.Log($"Fetched background job <{job.Id}> from environment <{connection.Environment}>");
            return job;
        }
        /// <inheritdoc/>
        public async Task<ILockedBackgroundJob> GetWithLockAsync(IClientConnection connection, string id, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgument(nameof(id));
            connection.ValidateArgument(nameof(connection));
            var clientConnection = GetClientStorageConnection(connection);

            _logger.Log($"Fetching background job <{id}> from environment <{connection.Environment}> with write lock");

            // Try lock first
            var jobLock = await _service.LockAsync(id, clientConnection.StorageConnection, requester, token).ConfigureAwait(false);

            _logger.Debug($"Got lock on background job <{id}> from environment <{connection.Environment}> for <{jobLock.LockedBy}>");

            // Then fetch
            var jobStorage = await _service.GetAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);
            var job = new BackgroundJob(connection.StorageConnection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, true);

            _logger.Log($"Fetched background job <{job.Id}> from environment <{connection.Environment}> with write lock for <{job?.Lock?.LockedBy}>");
            return job;
        }
        /// <inheritdoc/>
        public async Task<IClientQueryResult<ILockedBackgroundJob>> DequeueAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder, int limit = 100, string requester = null, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            conditionBuilder.ValidateArgument(nameof(conditionBuilder));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Trying the lock the next <{limit}> in environment <{connection.Environment}>");

            var queryConditions = new BackgroundJobQueryConditions(conditionBuilder);

            var (lockedJobs, total) = await _service.LockAsync(connection.StorageConnection, queryConditions, limit, requester, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach (var result in lockedJobs)
                {
                    backgroundJobs.Add(new BackgroundJob(connection.StorageConnection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, true));
                }

                _logger.Log($"Dequeued <{lockedJobs.Length}> background jobs of the total <{total}> jobs matching the query condition");
                return new QueryResult<ILockedBackgroundJob>(backgroundJobs, total);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                foreach (var job in backgroundJobs)
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
        public async Task<IClientQueryResult<IReadOnlyBackgroundJob>> QueryAsync(IClientConnection connection, Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> conditionBuilder = null, int pageSize = 1000, int page = 1, QueryBackgroundJobOrderByTarget? orderBy = null, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Querying background jobs in environment <{connection.Environment}>");

            var queryConditions = conditionBuilder != null ? new BackgroundJobQueryConditions(conditionBuilder) : new BackgroundJobQueryConditions();

            var (results, total) = await _service.SearchAsync(connection.StorageConnection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<BackgroundJob> backgroundJobs = new List<BackgroundJob>();

            try
            {
                foreach(var result in results)
                {
                    backgroundJobs.Add(new BackgroundJob(connection.StorageConnection, _serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, result, false));
                }

                _logger.Log($"Query returned <{results.Length}> background jobs of the total <{total}> jobs matching the query condition");
                return new QueryResult<IReadOnlyBackgroundJob>(backgroundJobs, total);
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

            _logger.Log($"Querying background jobs in environment <{connection.Environment}> to get the amount matching the query condition");

            var queryConditions = conditionBuilder != null ? new BackgroundJobQueryConditions(conditionBuilder) : new BackgroundJobQueryConditions();

            var matching = await _service.CountAsync(connection.StorageConnection, queryConditions, token).ConfigureAwait(false);

            _logger.Log($"There are <{matching}> background jobs in environment <{connection.Environment}> that match the query condition");
            return matching;
        }

        private async Task<string> CreateAsync(ClientStorageConnection clientConnection, InvocationInfo invocationInfo, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            clientConnection.ValidateArgument(nameof(clientConnection));
            invocationInfo.ValidateArgument(nameof(invocationInfo));


            var builder = new JobBuilder(this, clientConnection, jobBuilder);
            var job = new BackgroundJob(_serviceProvider.CreateAsyncScope(), _options.Get(clientConnection.Environment), clientConnection.Environment, builder.Queue, builder.Priority, invocationInfo, builder.Properties, builder.Middleware);
            // Dispose job after connection closes
            clientConnection.OnDispose(async () => await job.DisposeAsync());

            IBackgroundJobState state = builder.ElectionState;
            // Move to enqueued state by default if no custom state is set
            if (state == null) state = new EnqueuedState();

            // Transition to initial state
            _logger.Debug($"Triggering state election for new job to transition into state <{builder.ElectionState}>");
            await job.ChangeStateAsync(state, token);

            // Save changes
            await job.SaveChangesAsync(clientConnection.StorageConnection, false, token);
            _logger.Log($"Created job <{job.Id}> in environment <{clientConnection.Environment}>");

            return job.Id;
        }

        #region Classes
        private class JobBuilder : IBackgroundJobBuilder
        {
            // Fields
            private readonly List<MiddlewareInfo> _middleware = new List<MiddlewareInfo>();
            private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

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

            public JobBuilder(IBackgroundJobClient client, IClientConnection connection, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> configurator)
            {
                Client = client.ValidateArgument(nameof(client));
                Connection = connection.ValidateArgument(nameof(connection));

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
                _middleware.Add(new MiddlewareInfo(typeof(T), context, priority));
                return this;
            }
        }

        private class QueryResult<T> : IClientQueryResult<T>
        {
            /// <inheritdoc/>
            public long Total { get; }
            /// <inheritdoc/>
            public T[] Results { get; }

            public QueryResult(IEnumerable<T> results, long total)
            {
                Results = results.ValidateArgument(nameof(results)).ToArray();
                Total = total;
            }
            /// <inheritdoc/>
            public async ValueTask DisposeAsync()
            {
                var exceptions = new List<Exception>();

                Results.ForceExecute(x =>
                {
                    if(x is BackgroundJob job)
                    {
                        job.Cancel();
                    }
                });

                // Use dedicated thread if possible for faster dispse
                await Task.Factory.StartNew(async () =>
                {
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
                }, TaskCreationOptions.LongRunning).Unwrap().ConfigureAwait(false);

                

                if (exceptions.HasValue()) throw new AggregateException(exceptions);
            }
        }
        #endregion
    }
}
