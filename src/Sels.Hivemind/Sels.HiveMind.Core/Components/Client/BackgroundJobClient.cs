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
using Sels.HiveMind.Models.Queue;
using Sels.HiveMind.Storage;
using Sels.Core.Extensions.Reflection;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Service.Job;

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
            var job = new BackgroundJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, false);

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
            _ = await _service.LockAsync(id, clientConnection.StorageConnection, requester, token).ConfigureAwait(false);

            // Then fetch
            var jobStorage = await _service.GetAsync(id, clientConnection.StorageConnection, token).ConfigureAwait(false);
            var job = new BackgroundJob(_serviceProvider.CreateAsyncScope(), _options.Get(connection.Environment), connection.Environment, jobStorage, false);

            _logger.Log($"Fetched background job <{job.Id}> from environment <{connection.Environment}> with write lock for <{job?.Lock?.LockedBy}>");
            return job;
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

                InQueue(HiveMindConstants.Job.DefaultQueue, QueuePriority.Normal);
                if(configurator != null) configurator(this);
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
        #endregion
    }
}
