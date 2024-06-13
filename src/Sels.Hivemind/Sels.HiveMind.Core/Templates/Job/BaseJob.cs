using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Dispose;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.Core.Mediator;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Client;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Actions;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Templates.Job
{
    /// <summary>
    /// Base class for all jobs.
    /// </summary>
    /// <typeparam name="TClient">The type of client used by the job</typeparam>
    /// <typeparam name="TService">The type of service used by the background job</typeparam>
    /// <typeparam name="TJob">The type to return for the fluent syntax</typeparam>
    /// <typeparam name="TChangeLog">The type of changelog used to keep track of changes</typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TStateStorageData">The type of storage data used to store the job states</typeparam>
    /// <typeparam name="TJobStorageData">The type of storage data used by the job</typeparam>
    /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
    /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
    public abstract class BaseJob<TClient, TService, TLockedJob, TChangeLog, TChangeTracker, TJobStorageData, TState, TStateStorageData, TAction> : ILockedJob<TLockedJob, TChangeTracker, TState, TAction>, IAsyncExposedDisposable
        where TState : class, IJobState
        where TChangeLog : IJobChangeLog<TState>, TChangeTracker
        where TChangeTracker : IJobChangeTracker<TState>
        where TClient : IClient
        where TService : IJobService<TJobStorageData, TState, TStateStorageData>
        where TJobStorageData : JobStorageData
        where TStateStorageData : JobStateStorageData, new()
    {
        // Fields
        /// <summary>
        /// Thread lock used to synchronize state changes.
        /// </summary>
        protected readonly object _lock = new object();
        /// <summary>
        /// Async thread lock to synchronize action execution.
        /// </summary>
        protected readonly SemaphoreSlim _actionLock = new SemaphoreSlim(1, 1);
        /// <summary>
        /// Service scope used to resolve services scoped to the current job.
        /// </summary>
        protected readonly AsyncServiceScope _resolverScope;
        /// <summary>
        /// The options configured for the current HiveMind environment of the job.
        /// </summary>
        protected readonly HiveMindOptions _options;

        // State
        /// <summary>
        /// True if the current instance if the owner of the lock, otherwise false.
        /// </summary>
        protected bool _hasLock;
        protected List<JobStateInfo<TService, TJobStorageData, TState, TStateStorageData>> _states;
        /// <summary>
        /// The current properties of the job.
        /// </summary>
        protected LazyPropertyInfoDictionary _properties;
        /// <summary>
        /// The current state of the lock on the job.
        /// </summary>
        protected LockStorageData _lockData;
        /// <summary>
        /// Contains the invocation data on how to execute the current job.
        /// </summary>
        protected InvocationInfo _invocation;
        /// <summary>
        /// The middleware attached to the job.
        /// </summary>
        protected List<MiddlewareInfo> _middleware;

        // Properties
        /// <inheritdoc/>
        public Guid ExecutionId { get; protected set; }
        /// <inheritdoc/>
        public string Id { get; protected set; }
        /// <inheritdoc/>
        public string Environment { get; protected set; }
        /// <inheritdoc/>
        public string Queue { get; protected set; }
        /// <inheritdoc/>
        public QueuePriority Priority { get; protected set; }
        /// <inheritdoc/>
        public DateTime CreatedAtUtc { get; protected set; }
        /// <inheritdoc/>
        public DateTime ModifiedAtUtc { get; protected set; }
        /// <inheritdoc/>
        public ILockInfo Lock => _lockData;
        /// <inheritdoc/>
        public bool HasLock => _hasLock && (Lock?.LockedBy.HasValue() ?? false);
        /// <inheritdoc/>
        public TState State => _states?.FirstOrDefault()?.State;
        /// <inheritdoc/>
        public IEnumerable<TState> StateHistory => _states?.Skip(1).Reverse().Select(x => x.State);
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties => _properties;
        private IDictionary<string, object> WriteableProperties => _properties;
        /// <inheritdoc/>
        public IInvocationInfo Invocation => _invocation;
        /// <inheritdoc/>
        public IReadOnlyList<IMiddlewareInfo> Middleware => _middleware;

        /// <inheritdoc/>
        public TChangeTracker ChangeTracker => ChangeLog;
        /// <summary>
        /// The change log used to keep track of changes.
        /// </summary>
        protected abstract TChangeLog ChangeLog { get; set; }
        /// <summary>
        /// The instance to return for the fluent syntax.
        /// </summary>
        protected abstract TLockedJob Job { get; }

        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public abstract TJobStorageData StorageData { get; }

        // State
        /// <inheritdoc/>
        public bool IsDeleted { get; set; }
        /// <summary>
        /// True if the current background job is currently in a transaction, otherwise false.
        /// </summary>
        public bool IsCommiting { get; set; }
        /// <summary>
        /// Indicates if the current instance is for the creation of the job.
        /// </summary>
        protected bool IsCreation { get; set; }
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }

        // Services
        /// <summary>
        /// Lazy loaded client used to open connections.
        /// </summary>
        protected Lazy<TClient> JobClient { get; }
        /// <summary>
        /// Lazy loaded service used to perform actions on the job.
        /// </summary>
        protected Lazy<TService> JobService { get; }
        /// <summary>
        /// Lazy loaded notifier used to send events and requests.
        /// </summary>
        protected Lazy<INotifier> Notifier { get; }
        /// <summary>
        /// Lazy loaded cache used to speed up conversion.
        /// </summary>
        protected Lazy<IMemoryCache> Cache { get; }
        /// <summary>
        /// Lazy loaded logger used to log messages.
        /// </summary>
        protected Lazy<ILogger> LazyLogger { get; }
        /// <summary>
        /// Optional logger for tracing.
        /// </summary>
        protected ILogger Logger => LazyLogger.Value;

        /// <inheritdoc cref="BaseJob{TState, TAction}"/>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        /// <param name="resolverScope"><inheritdoc cref="_resolverScope"/></param>
        /// <param name="environment">The environment <paramref name="storageData"/> was retrieved from</param>
        /// <param name="storageData">The persisted state of the job</param>
        /// <param name="hasLock">True if the job was fetched with a lock, otherwise false for only reading the job</param>
        protected BaseJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment, TJobStorageData storageData, bool hasLock) : this(resolverScope, options)
        {
            _options = options.ValidateArgument(nameof(options));
            _resolverScope = resolverScope;

            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Set(storageData.ValidateArgument(nameof(storageData)));

            if (hasLock)
            {
                if (Lock == null) throw new InvalidOperationException($"Job is supposed to be locked but lock state is missing");
                _hasLock = true;
            }
        }

        /// <inheritdoc cref="BaseJob{TState, TAction}"/>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        /// <param name="resolverScope"><inheritdoc cref="_resolverScope"/></param>
        /// <param name="environment">The environment <paramref name="storageData"/> was retrieved from</param>
        protected BaseJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment) : this(resolverScope, options)
        {
            _options = options.ValidateArgument(nameof(options));
            _resolverScope = resolverScope;

            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }

        private BaseJob(AsyncServiceScope resolverScope, HiveMindOptions options)
        {
            _options = options.ValidateArgument(nameof(options));
            _resolverScope = resolverScope;
            JobClient = new Lazy<TClient>(() => _resolverScope.ServiceProvider.GetRequiredService<TClient>(), LazyThreadSafetyMode.ExecutionAndPublication);
            JobService = new Lazy<TService>(() => _resolverScope.ServiceProvider.GetRequiredService<TService>(), LazyThreadSafetyMode.ExecutionAndPublication);
            Notifier = new Lazy<INotifier>(() => _resolverScope.ServiceProvider.GetRequiredService<INotifier>(), LazyThreadSafetyMode.ExecutionAndPublication);
            Cache = new Lazy<IMemoryCache>(() => _resolverScope.ServiceProvider.GetRequiredService<IMemoryCache>(), LazyThreadSafetyMode.ExecutionAndPublication);
            LazyLogger = new Lazy<ILogger>(() => _resolverScope.ServiceProvider.GetService<ILogger<BackgroundJob>>(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <inheritdoc/>
        public void ChangeQueue(string queue, QueuePriority priority)
        {
            queue.ValidateArgument(nameof(queue));

            lock (_lock)
            {
                if (!queue.Equals(Queue, StringComparison.OrdinalIgnoreCase))
                {
                    Queue = queue;
                    if (!ChangeLog.QueueChanged) ChangeLog.QueueChanged = true;
                }
                if (Priority != priority)
                {
                    Priority = priority;
                    if (!ChangeLog.PriorityChanged) ChangeLog.PriorityChanged = true;
                }
            }
        }

        #region Cancellation
        /// <inheritdoc/>
        public async Task<bool?> CancelAsync(IStorageConnection connection, string requester = null, string reason = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot cancel {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (IsDeleted) throw new InvalidOperationException($"Cannot cancel deleted background job");

            var cancelRequester = requester.HasValue() ? requester : Guid.NewGuid().ToString();
            Logger.Log($"Trying to cancel background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{cancelRequester}>", Id, Environment);

            if (!CanCancelJob())
            {
                return null;
            }

            var (wasLocked, _) = await TryLockAsync(connection, cancelRequester, token).ConfigureAwait(false);

            // State could have changed after locking
            if (!CanCancelJob())
            {
                return null;
            }

            if (wasLocked)
            {
                Logger.Debug($"Got lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Cancelling for <{cancelRequester}>", Id, Environment);

                await SetCancelledStateAsync(connection, reason, token).ConfigureAwait(false);

                Logger.Debug($"Cancelled background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{cancelRequester}>", Id, Environment);
                return true;
            }
            else
            {
                Logger.Debug($"Coud not get a lock background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to cancel for <{cancelRequester}>. Scheduling action", Id, Environment);

                await ScheduleCancellationAction(connection, reason, token).ConfigureAwait(false);
                Logger.Debug($"Scheduled action to cancel background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{cancelRequester}>", Id, Environment);
                return false;
            }
        }
        /// <summary>
        /// Sets the current job state to cancelled.
        /// </summary>
        /// <param name="connection">The connection to use to set the cancelled state</param>
        /// <param name="reason">Why the job is being cancelled</param>
        /// <param name="token">Token that will called when the action is requested to stop</param>
        protected abstract Task SetCancelledStateAsync(IStorageConnection connection, string reason, CancellationToken token);
        /// <summary>
        /// Schedules the action to cancel the current job.
        /// </summary>
        /// <param name="connection">The connection to use to schedule the action</param>
        /// <param name="reason">Why the job is being cancelled</param>
        /// <param name="token">Token that will called when the action is requested to stop</param>
        protected abstract Task ScheduleCancellationAction(IStorageConnection connection, string reason, CancellationToken token);
        /// <summary>
        /// Checks if the current job is in a state where it can be cancelled.
        /// </summary>
        /// <returns>True if the job can be cancelled, otherwise false</returns>
        protected abstract bool CanCancelJob();
        /// <inheritdoc/>
        public async Task<bool?> CancelAsync(string requester = null, string reason = null, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for job <{HiveLog.Job.Id}> to delete job", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                return await CancelAsync(connection.StorageConnection, requester, reason, token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Refresh
        /// <summary>
        /// Sets the current state of the job equal to the state in <paramref name="data"/>.
        /// </summary>
        /// <param name="data">The state to set from</param>
        protected virtual void Set(TJobStorageData data)
        {
            data.ValidateArgument(nameof(data));

            Id = data.Id;
            ExecutionId = data.ExecutionId;
            Queue = data.Queue;
            Priority = data.Priority;
            CreatedAtUtc = data.CreatedAtUtc;
            ModifiedAtUtc = data.ModifiedAtUtc;
            Set(data.Lock);
            Set(data.Properties);
            Set(data.Middleware);
            Set(data.InvocationData);
        }
        /// <summary>
        /// Sets the current lock state equal to the state in <paramref name="data"/>
        /// </summary>
        /// <param name="data">The state to set</param>
        protected void Set(LockStorageData data)
        {
            if (data == null)
            {
                _lockData = null;
                _hasLock = false;
                return;
            }

            _lockData = data;
        }
        private void Set(IEnumerable<StorageProperty> properties)
        {
            if (properties != null)
            {
                _properties = new LazyPropertyInfoDictionary(properties.Select(x => new LazyPropertyInfo(x, _options, Cache.Value)), _options, Cache.Value);
            }
            else
            {
                _properties = new LazyPropertyInfoDictionary(_options, Cache.Value);
            }
        }
        /// <summary>
        /// Sets the current middleware state equal to the state in <paramref name="data"/>
        /// </summary>
        /// <param name="data">The state to set</param>
        protected void Set(IEnumerable<MiddlewareStorageData> middleware)
        {
            _middleware = middleware != null ? middleware.Select(x => new MiddlewareInfo(x, _options, Cache.Value)).ToList() : new List<MiddlewareInfo>();
        }
        /// <summary>
        /// Sets the current invocation state equal to the state in <paramref name="data"/>
        /// </summary>
        /// <param name="data">The state to set</param>
        protected void Set(InvocationStorageData data)
        {
            data.ValidateArgument(nameof(data));

            _invocation = new InvocationInfo(data, _options, Cache.Value);
        }
        /// <inheritdoc/>
        public async Task RefreshAsync(CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for job <{HiveLog.Job.Id}> to refresh job", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                await RefreshAsync(connection.StorageConnection, token).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async Task RefreshAsync(IStorageConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot refresh state for {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            await using var lockScope = await _actionLock.LockAsync(token);
            
            await RefreshNoLockAsync(connection, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the state of the current job to get the latest changes.
        /// Does not acquire the action lock before refreshing.
        /// </summary>
        /// <param name="connection">The connection to use to perform the lock with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns>Task containing the execution state</returns>
        protected async Task RefreshNoLockAsync(IStorageConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot refresh state for {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot refresh state on new job");
            if (IsDeleted) throw new InvalidOperationException($"Cannot refresh deleted job");

            Logger.Log($"Refreshing state for job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            var currentLockHolder = Lock?.LockedBy;

            var currentState = await JobService.Value.GetAsync(Id, connection, token).ConfigureAwait(false);

            // Check if lock is still valid
            if (currentLockHolder != null && !currentLockHolder.EqualsNoCase(currentState?.Lock?.LockedBy))
            {
                lock (_lock)
                {
                    _hasLock = false;
                }
            }

            lock (_lock)
            {
                Set(currentState);
            }

            Logger.Log($"Refreshed state for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

        }
        #endregion

        #region Property
        /// <inheritdoc/>
        public T GetProperty<T>(string name)
        {
            name.ValidateArgument(nameof(name));

            lock (_lock)
            {
                if (TryGetProperty<T>(name, out var value))
                {
                    return value;
                }
            }

            throw new InvalidOperationException($"Job does not have a property with name <{name}>");
        }
        /// <inheritdoc/>
        public void SetProperty<T>(string name, T value)
        {
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            lock (_lock)
            {
                var exists = WriteableProperties.ContainsKey(name);

                if (!exists)
                {
                    WriteableProperties.Add(name, value);
                    return;
                }
                // Same as deleting
                else if (value == null)
                {
                    RemoveProperty(name);
                }
                else
                {
                    WriteableProperties[name] = value;
                }

                if (exists)
                {
                    if (!ChangeLog.NewProperties.Contains(name, StringComparer.OrdinalIgnoreCase) && !ChangeLog.UpdatedProperties.Contains(name, StringComparer.OrdinalIgnoreCase)) ChangeLog.UpdatedProperties.Add(name);
                }
                else if (ChangeLog.RemovedProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    ChangeLog.UpdatedProperties.Add(name);
                    ChangeLog.RemovedProperties.Remove(name);
                }
                else
                {
                    ChangeLog.NewProperties.Add(name);
                }
            }
        }
        /// <inheritdoc/>
        public bool TryGetProperty<T>(string name, out T value)
        {
            name.ValidateArgument(nameof(name));
            value = default;

            lock (_lock)
            {
                if (WriteableProperties.ContainsKey(name))
                {
                    value = WriteableProperties[name].ConvertTo<T>();
                    return true;
                }
            }
            return false;
        }
        /// <inheritdoc/>
        public void RemoveProperty(string name)
        {
            name.ValidateArgument(nameof(name));

            lock (_lock)
            {
                if (_properties.ContainsKey(name))
                {
                    WriteableProperties.Remove(name);

                    if (!ChangeLog.NewProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        ChangeLog.RemovedProperties.Add(name);
                        if (ChangeLog.UpdatedProperties.Contains(name, StringComparer.OrdinalIgnoreCase)) ChangeLog.UpdatedProperties.Remove(name);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Job does not have a property with name <{name}>");
                }
            }
        }
        #endregion

        #region State
        /// <inheritdoc/>
        public async Task<bool> ChangeStateAsync(IStorageConnection storageConnection, TState state, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            await using var lockScope = await _actionLock.LockAsync(token);

            var originalElected = await ChangeStateNoLockAsync(storageConnection, state, false, token).ConfigureAwait(false);

            return originalElected;
        }
        /// <summary>
        /// Triggers state election to try and change the state of the job to <paramref name="state"/>.
        /// Does not try to acquire the action lock before changing state.
        /// </summary>
        /// <param name="storageConnection">Optional connection to change the state with. Gives handlers access to the same transaction</param>
        /// <param name="state">The state to transition into</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the current state was changed to <paramref name="state"/>, false if state election resulted in another state being elected</returns>
        protected async Task<bool> ChangeStateNoLockAsync(IStorageConnection storageConnection, TState state, bool isForDeletion = false, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            if (IsDeleted && !isForDeletion) throw new InvalidOperationException($"Cannot change state on deleted job");
            state.ValidateArgument(nameof(state));
            Logger.Log($"Starting state election for job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to transition into state <{HiveLog.Job.State}>", Id, Environment, state.Name);

            bool elected = false;
            bool originalElected = true;
            do
            {
                // Set state
                Logger.Debug($"Applying state <{HiveLog.Job.State}> on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.Name, Id, Environment);
                await ApplyStateAsync(storageConnection, state, token).ConfigureAwait(false);

                // Try and elect state as final
                Logger.Debug($"Trying to elect state <{HiveLog.Job.State}> on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> as final", state.Name, Id, Environment);
                var result = await RaiseStateElectionRequest(storageConnection, state, token).ConfigureAwait(false);

                if (result.Completed)
                {
                    originalElected = false;
                    state = result.Response;
                    Logger.Debug($"State election resulted in new state <{HiveLog.Job.State}> for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.Name, Id, Environment);
                }
                else
                {
                    elected = true;
                }
            }
            while (!elected);


            Logger.Log($"Final state <{HiveLog.Job.State}> elected for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.Name, Id, Environment);
            return originalElected;
        }

        private async Task ApplyStateAsync(IStorageConnection storageConnection, TState state, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            state.ValidateArgument(nameof(state));

            SetState(state);

            await RaiseStateAppliedEvent(storageConnection, state, token).ConfigureAwait(false);
            ExecutionId = Guid.NewGuid();
        }
        /// <summary>
        /// Raises a request to elect a new state as final for the current state.
        /// </summary>
        /// <param name="storageConnection">Optional connection that can be used during the raising of the request</param>
        /// <param name="state">The state the election is started for</param>
        /// <param name="token">Token that can be cancelled when the action is requested to stop</param>
        protected abstract Task<RequestResponse<TState>> RaiseStateElectionRequest(IStorageConnection storageConnection, TState state, CancellationToken token = default);
        /// <summary>
        /// Raises an event that a state was applied to the current job.
        /// </summary>
        /// <param name="storageConnection">Optional connection that can be used during the raising of the event</param>
        /// <param name="state">The state that was applied</param>
        /// <param name="token">Token that can be cancelled when the action is requested to stop</param>
        protected abstract Task RaiseStateAppliedEvent(IStorageConnection storageConnection, TState state, CancellationToken token = default);
        /// <summary>
        /// Sets <paramref name="state"/> as the current state.
        /// </summary>
        /// <param name="state">The state to set</param>
        protected void SetState(TState state)
        {
            state.ValidateArgument(nameof(state));

            lock (_lock)
            {
                _states ??= new List<JobStateInfo<TService, TJobStorageData, TState, TStateStorageData>>();
                _states.Insert(0, new JobStateInfo<TService, TJobStorageData, TState, TStateStorageData>(state, JobService, Environment));
                ChangeLog.NewStates.Add(state);
                state.ElectedDateUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Raises an event that a final state was elected on the current job.
        /// </summary>
        /// <param name="storageConnection">Optional connection that can be used during the raising of the event</param>
        /// <param name="state">The state that was elected</param>
        /// <param name="token">Token that can be cancelled when the action is requested to stop</param>
        protected abstract Task RaiseFinalStateElectedEvent(IStorageConnection storageConnection, TState state, CancellationToken token = default);
        #endregion

        #region Action
        /// <inheritdoc/>
        public async Task ScheduleAction(IStorageConnection connection, Type actionType, object actionContext, bool forceExecute = false, byte priority = 255, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot schedule action on {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            actionType.ValidateArgument(nameof(actionType));
            actionType.ValidateArgumentAssignableTo(nameof(actionType), typeof(TAction));
            await using var lockScope = await _actionLock.LockAsync(token);
            if (IsDeleted) throw new InvalidOperationException($"Cannot schedule action on deleted job");

            Logger.Log($"Scheduling action of type <{actionType}> on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            var action = new ActionInfo()
            {
                ComponentId = Id,
                Type = actionType,
                Context = actionContext,
                ForceExecute = forceExecute,
                ExecutionId = ExecutionId,
                Priority = priority,
                CreatedAtUtc = DateTime.UtcNow
            };

            await JobService.Value.CreateActionAsync(connection, action, token).ConfigureAwait(false);

            Logger.Log($"Scheduled action <{action.Id}> of type <{actionType}> on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
        }
        /// <inheritdoc/>
        public async Task ScheduleAction(Type actionType, object actionContext, bool forceExecute = false, byte priority = 255, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for job {HiveLog.Job.Id} to schedule action", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                await ScheduleAction(connection.StorageConnection, actionType, actionContext, forceExecute, priority, token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Data
        /// <inheritdoc/>
        public abstract Task<IAsyncDisposable> AcquireStateLock(IStorageConnection connection, CancellationToken token = default);
        /// <inheritdoc/>
        public async Task<(bool Exists, T Data)> TryGetDataAsync<T>(IStorageConnection connection, string name, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot fetch data <{name}> from {this} in environment {Environment} with storage connection to environment {connection.Environment}");

            Logger.Log($"Trying to fetch data <{name}> from job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            return await JobService.Value.TryGetDataAsync<T>(connection, Id, name, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task<(bool Exists, T Data)> TryGetDataAsync<T>(string name, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for job {HiveLog.Job.Id} to fetch data <{name}>", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                return await TryGetDataAsync<T>(connection.StorageConnection, name, token).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async Task SetDataAsync<T>(IStorageConnection connection, string name, T value, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot fetch data <{name}> from {this} in environment {Environment} with storage connection to environment {connection.Environment}");

            Logger.Log($"Saving data <{name}> to job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            await JobService.Value.SetDataAsync(connection, Id, name, value, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task SetDataAsync<T>(string name, T value, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for job {HiveLog.Job.Id} to save data <{name}>", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                await SetDataAsync(connection.StorageConnection, name, value, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Lock
        /// <inheritdoc/>
        public async Task<bool> SetHeartbeatAsync(CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            await using var lockScope = await _actionLock.LockAsync(token);
            string holder;
            lock (_lock)
            {
                if (IsDeleted) return false;
                if (!HasLock) return false;
                holder = Lock.LockedBy;
            }

            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for job <{HiveLog.Job.Id}> to set heartbeat on lock", Environment, Id);
            try
            {
                LockStorageData lockState = null;
                await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
                {
                    Logger.Debug($"Updating heartbeat in storage for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
                    lockState = await JobService.Value.HeartbeatLockAsync(Id, holder, connection.StorageConnection, token).ConfigureAwait(false);
                    await connection.CommitAsync(token).ConfigureAwait(false);

                    Logger.Debug($"Heartbeat in storage for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> has been set to <{lockState.LockHeartbeatUtc.ToLocalTime()}>");
                }

                lock (_lock)
                {
                    _hasLock = true;
                    Set(lockState);
                }
                return true;
            }
            catch (JobAlreadyLockedException)
            {
                lock (_lock)
                {
                    _hasLock = false;
                }

                return false;
            }
        }
        /// <inheritdoc/>
        public async Task EnsureValidLockAsync(CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);

            await ValidateLock(token).ConfigureAwait(false);
        }
        /// <summary>
        /// Checks if the current lock is still valid and tries to set the heartbeat if it's within the safety offset.
        /// </summary>
        /// <param name="token">Optional token used to cancel the request</param>
        /// <exception cref="JobLockStaleException"></exception>
        protected async Task ValidateLock(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);

            if (!await MaintainLock(token).ConfigureAwait(false))
            {
                throw new JobLockStaleException(Id, Environment);
            }
        }
        /// <summary>
        /// Makes sure the current lock is still valid and tries to set the heartbeat if it's within the safety offset.
        /// </summary>
        /// <param name="token">Optional token used to cancel the request</param>
        /// <returns>True if the lock is still valid, otherwise false</returns>
        protected async Task<bool> MaintainLock(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            bool inSafetyOffset = false;
            lock (_lock)
            {
                // Check if we have lock
                if (!HasLock || Lock == null) return false;
                // Check if we are within the safety offset try to set the heartbeat
                if (DateTime.UtcNow >= Lock.LockHeartbeatUtc.Add(_options.LockTimeout) - _options.LockExpirySafetyOffset)
                {
                    inSafetyOffset = true;
                }
            }

            bool isStale = false;
            if (inSafetyOffset)
            {
                Logger.Warning($"Lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is within the safety offset. Trying to extend lock", Id, Environment);
                try
                {
                    if (!await SetHeartbeatAsync(token).ConfigureAwait(false))
                    {
                        isStale = true;
                    }
                }
                catch (JobNotFoundException)
                {
                    isStale = true;
                }
            }

            return !isStale;
        }

        /// <summary>
        /// Unlocks the current job if it still holds the lock.
        /// </summary>
        /// <param name="connection">The connection to use to unlock the job</param>
        protected abstract Task TryUnlockJobAsync(IStorageConnection connection);

        /// <summary>
        /// Signals the job that it no longer holds the lock.
        /// </summary>
        public void RemoveHoldOnLock()
        {
            lock (_lock)
            {
                _hasLock = false;
            }
        }
        #endregion

        #region Locking
        /// <inheritdoc/>
        public async Task<TLockedJob> LockAsync(string requester = null, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for {HiveLog.Job.Id} to lock job", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                return await LockAsync(connection.StorageConnection, requester, token).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async Task<TLockedJob> LockAsync(IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot lock {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            await using var lockScope = await _actionLock.LockAsync(token);
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot lock a new job");
            if (IsDeleted) throw new InvalidOperationException($"Cannot lock deleted job");

            var hasLock = HasLock;
            if (hasLock) hasLock = await MaintainLock(token).ConfigureAwait(false);

            if (!hasLock)
            {
                Logger.Log($"Trying to acquire exclusive lock on job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for <{requester ?? "RANDOM"}>", Id, Environment);

                var lockState = await JobService.Value.LockAsync(Id, connection, requester, token).ConfigureAwait(false);

                lock (_lock)
                {
                    Set(lockState);
                    _hasLock = true;
                }

                Logger.Log($"Job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
            }
            else
            {
                Logger.Log(HasLock ? LogLevel.Information : LogLevel.Warning, $"Job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> already locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
            }

            // We didn't have lock before so refresh state as changes could have been made
            if (!hasLock)
            {
                await RefreshAsync(connection, token).ConfigureAwait(false);
            }

            return Job;
        }
        /// <inheritdoc/>
        public async Task<(bool WasLocked, TLockedJob LockedJob)> TryLockAsync(IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot lock {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            bool wasLocked = false;
            var hasLock = HasLock;
            {
                await using var lockScope = await _actionLock.LockAsync(token);
                if (!Id.HasValue()) throw new InvalidOperationException($"Cannot lock a new job");
                if (IsDeleted) throw new InvalidOperationException($"Cannot lock deleted backound job");


                if (hasLock) hasLock = await MaintainLock(token).ConfigureAwait(false);

                if (!hasLock)
                {
                    Logger.Log($"Trying to acquire exclusive lock on job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for <{requester ?? "RANDOM"}>", Id, Environment);

                    wasLocked = await JobService.Value.TryLockAsync(Id, connection, requester, token).ConfigureAwait(false);
                    _hasLock = wasLocked;

                    Logger.Log($"Job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
                }
                else if (HasLock)
                {
                    Logger.Log(LogLevel.Information, $"Job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> already locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
                    wasLocked = true;
                }
            }

            // If we didn't have state lock before state could have changed so refresh
            if (!hasLock)
            {
                await RefreshAsync(connection, token).ConfigureAwait(false);
            }

            if (!wasLocked)
            {
                Logger.Log(LogLevel.Information, $"Job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> already locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
            }

            return (wasLocked, wasLocked ? Job : default);
        }
        /// <inheritdoc/>
        public async Task<(bool WasLocked, TLockedJob LockedJob)> TryLockAsync(string requester = null, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for {HiveLog.Job.Id} to try lock job", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                return await TryLockAsync(connection.StorageConnection, requester, token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Delete
        /// <inheritdoc/>
        public async Task<bool> SystemDeleteAsync(IStorageConnection connection, string reason = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot delete {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            await using var lockScope = await _actionLock.LockAsync(token);
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot delete new job");
            if (IsDeleted) throw new InvalidOperationException($"Cannot delete an already deleted job");

            Logger.Log($"Permanently deleting job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            await ValidateLock(token).ConfigureAwait(false);
            Logger.Debug($"Starting election to deleting state for job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            var deletingState = CreateSystemDeletingState();
            deletingState.Reason = reason;
            if (!await ChangeStateNoLockAsync(connection, deletingState, false, token).ConfigureAwait(false))
            {
                Logger.Debug($"Could not elect deleting state for job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Job will not be deleted", Id, Environment);
                return false;
            }

            // Delete
            if (!await TryDeleteJobAsync(connection, token).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Could not delete delete {this} in environment {Environment}");
            }

            lock (_lock)
            {
                IsDeleted = true;
            }

            var deletedState = CreateSystemDeletedState();
            deletedState.Reason = reason;
            if (!await ChangeStateNoLockAsync(connection, deletedState, true, token).ConfigureAwait(false))
            {
                Logger.Warning($"Could not elect deleted state for job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Checking if transaction can be aborted", Id, Environment);

                if (connection.HasTransaction)
                {
                    await connection.AbortTransactionAsync(token).ConfigureAwait(false);
                    IsDeleted = false;
                    await RefreshNoLockAsync(connection, token).ConfigureAwait(false);
                    return IsDeleted;
                }

                // No transaction so job was deleted
                IsDeleted = true;
                return IsDeleted;
            }

            if (IsDeleted)
            {
                await RaiseFinalStateElectedEvent(connection, State, token).ConfigureAwait(false);
            }

            Logger.Log($"Permanently deleted job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
            return IsDeleted;
        }
        /// <inheritdoc/>
        public async Task<bool> SystemDeleteAsync(string reason = null, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for job <{HiveLog.Job.Id}> to delete job", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                var delete = await SystemDeleteAsync(connection.StorageConnection, reason, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return delete;
            }
        }
        /// <summary>
        /// Returns the state that will be used to indicate that the current job is being permanently deleted.
        /// </summary>
        /// <returns>The state that will be used to indicate that the current job is being permanently deleted.</returns>
        protected abstract TState CreateSystemDeletingState();
        /// <summary>
        /// Returns the state that will be used to indicate that the current job was permanently deleted.
        /// </summary>
        /// <returns>The state that will be used to indicate that the current job was permanently deleted.</returns>
        protected abstract TState CreateSystemDeletedState();
        /// <summary>
        /// Tries to delete the current job taking into account locking.
        /// </summary>
        /// <param name="connection">The connectionto use to delete the job.</param>
        /// <param name="token">Token that will be cancelled when the current action is requested to stop.</param>
        /// <returns>True if the job was deleted, otherwise false.</returns>
        protected abstract Task<bool> TryDeleteJobAsync(IStorageConnection connection, CancellationToken token = default);
        #endregion

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            Logger.Debug($"Disposing job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            try
            {
                lock (_lock)
                {
                    if (IsDisposed.HasValue) return;
                    IsDisposed = false;
                }

                var exceptions = new List<Exception>();

                // Release lock
                try
                {
                    if (HasLock && Lock != null && !IsDeleted)
                    {
                        await using var lockScope = await _actionLock.LockAsync();
                        Logger.Debug($"Releasing lock on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

                        await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, true).ConfigureAwait(false))
                        {
                            await TryUnlockJobAsync(connection.StorageConnection).ConfigureAwait(false);

                            await connection.CommitAsync().ConfigureAwait(false);
                        }

                        Logger.Log($"Released lock on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                // Release services
                try
                {
                    await using var lockScope = await _actionLock.LockAsync();
                    Logger.Debug($"Disposing scope for job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
                    await _resolverScope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                if (exceptions.HasValue()) throw new AggregateException($"Could not properly dispose job <{Id}>", exceptions);
            }
            finally
            {
                IsDisposed = true;
            }
        }
    }
}
