using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.Core.Mediator;
using System.Linq;
using Sels.Core;
using Sels.Core.Dispose;
using Sels.Core.Scope.Actions;
using Sels.Core.Extensions.Threading;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Conversion.Extensions;
using Sels.HiveMind.Events.Job;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Service.Job;
using Sels.HiveMind.Requests.Job;
using Sels.Core.Extensions.Text;
using Sels.Core.Async.TaskManagement;
using Newtonsoft.Json.Linq;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Microsoft.Extensions.Caching.Memory;

namespace Sels.HiveMind
{
    /// <summary>
    /// Manages state of a background job.
    /// </summary>
    public class BackgroundJob : ILockedBackgroundJob, IAsyncExposedDisposable
    {
        // Contants
        private const string HeartbeatTaskName = "BackgroundJob.Lock.Heartbeat";

        // Fields
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly AsyncServiceScope _resolverScope;
        private Lazy<Dictionary<string, object>> _properties;
        private Lazy<IInvocationInfo> _invocation;
        private List<MiddlewareInfo> _middleware;
        private List<Delegates.Async.AsyncAction<CancellationToken>> _staleLockActions;
        private readonly HiveMindOptions _options;
        private Lazy<List<IBackgroundJobState>> _states;

        // Properties
        /// <inheritdoc/>
        public string Id { get; private set; }
        /// <inheritdoc/>
        public string Environment { get; private set; }
        /// <inheritdoc/>
        public Guid ExecutionId { get; set; }
        /// <inheritdoc/>
        public string Queue { get; private set; }
        /// <inheritdoc/>
        public QueuePriority Priority { get; private set; }
        /// <inheritdoc/>
        public DateTime CreatedAtUtc { get; private set; }
        /// <inheritdoc/>
        public DateTime ModifiedAtUtc { get; private set; }
        /// <inheritdoc/>
        public IBackgroundJobState State => _states?.Value.Last();
        /// <inheritdoc/>
        public IReadOnlyList<IBackgroundJobState> StateHistory => _states?.Value.Take(_states.Value.Count - 1).ToList();
        /// <inheritdoc/>
        public ILockInfo Lock { get; private set; }
        private bool HasLock { get; set; }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties => _properties.Value;
        /// <inheritdoc/>
        public IInvocationInfo Invocation => _invocation.Value;
        /// <inheritdoc/>
        public IReadOnlyList<IMiddlewareInfo> Middleware => _middleware ?? new List<MiddlewareInfo>();

        // Services
        private Lazy<IStorageProvider> StorageProvider { get; }
        private Lazy<IBackgroundJobService> BackgroundJobService { get; }
        private Lazy<ITaskManager> TaskManager { get; }
        private Lazy<INotifier> Notifier { get; }
        private Lazy<IMemoryCache> Cache { get; }
        private Lazy<ILogger> LazyLogger { get; }
        private ILogger Logger => LazyLogger.Value;

        // State
        private bool IsCreation { get; }
        /// <inheritdoc/>
        public bool NeedsDispose => Helper.Collection.Enumerate(StorageProvider.IsValueCreated, TaskManager.IsValueCreated, BackgroundJobService.IsValueCreated, Notifier.IsValueCreated, Cache.IsValueCreated, LazyLogger.IsValueCreated).Any(x => x == true);
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }
        /// <summary>
        /// Contains the changes made to the job.
        /// </summary>
        public BackgroundJobChangeLog ChangeLog { get; } = new BackgroundJobChangeLog();
        /// <inheritdoc/>
        IBackgroundJobChangesTracker IReadOnlyBackgroundJob.ChangeTracker => ChangeLog;

        /// <summary>
        /// Creates a new instance for creating a new job.
        /// </summary>
        /// <param name="resolverScope">Scope used to resolve services scoped to the lifetime of the job instance</param>
        /// <param name="environment">The environment the job will be created in</param>
        /// <param name="queue"><inheritdoc cref="Queue"/></param>
        /// <param name="priority"><inheritdoc cref="Priority"/></param>
        /// <param name="invocationInfo"><inheritdoc cref="Invocation"/></param>
        /// <param name="middleware"><inheritdoc cref="Middleware"/></param>
        /// <param name="properties"><inheritdoc cref="Properties"/></param>
        public BackgroundJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment, string queue, QueuePriority priority, IInvocationInfo invocationInfo, IReadOnlyDictionary<string, object> properties, IEnumerable<MiddlewareInfo> middleware) : this()
        {
            _options = options.ValidateArgument(nameof(options));

            _resolverScope = resolverScope;
            IsCreation = true;
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Queue = queue.ValidateArgument(nameof(queue));
            Priority = priority;
            _invocation = new Lazy<IInvocationInfo>(invocationInfo.ValidateArgument(nameof(invocationInfo)));
            _properties = new Lazy<Dictionary<string, object>>(() => properties.HasValue() ? properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), true);
            _middleware = middleware != null ? middleware.ToList() : new List<MiddlewareInfo>();

            SetState(new CreatedState());
            CreatedAtUtc = DateTime.UtcNow;
            ModifiedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new instance from a persisted job.
        /// </summary>
        /// <param name="connection">The connection that was used to fetch the job</param>
        /// <param name="resolverScope">Scope used to resolve services scoped to the lifetime of the job instance</param>
        /// <param name="environment">The environment <paramref name="storageData"/> was retrieved from</param>
        /// <param name="storageData">The persisted state of the job</param>
        /// <param name="hasLock">True if the job was fetches with a lock, otherwise false for only reading the job</param>
        public BackgroundJob(IStorageConnection connection, AsyncServiceScope resolverScope, HiveMindOptions options, string environment, JobStorageData storageData, bool hasLock) : this()
        {
            connection.ValidateArgument(nameof(connection));
            _options = options.ValidateArgument(nameof(options));
            _resolverScope = resolverScope;
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Set(storageData.ValidateArgument(nameof(storageData)));

            if (hasLock)
            {
                if (Lock == null) throw new InvalidOperationException($"Job is supposed to be locked but lock state is missing");
                HasLock = true;

                // Only keep lock alive if current transaction is commited
                if (connection.HasTransaction) connection.OnCommitted(StartKeepAliveTask);
                else _ = StartKeepAliveTask(CancellationToken.None);
            }
        }

        private BackgroundJob()
        {
            StorageProvider = new Lazy<IStorageProvider>(() => _resolverScope.ServiceProvider.GetRequiredService<IStorageProvider>(), LazyThreadSafetyMode.ExecutionAndPublication);
            TaskManager = new Lazy<ITaskManager>(() => _resolverScope.ServiceProvider.GetService<ITaskManager>(), LazyThreadSafetyMode.ExecutionAndPublication);
            BackgroundJobService = new Lazy<IBackgroundJobService>(() => _resolverScope.ServiceProvider.GetRequiredService<IBackgroundJobService>(), LazyThreadSafetyMode.ExecutionAndPublication);
            Notifier = new Lazy<INotifier>(() => _resolverScope.ServiceProvider.GetRequiredService<INotifier>(), LazyThreadSafetyMode.ExecutionAndPublication);
            Cache = new Lazy<IMemoryCache>(() => _resolverScope.ServiceProvider.GetRequiredService<IMemoryCache>(), LazyThreadSafetyMode.ExecutionAndPublication);
            LazyLogger = new Lazy<ILogger>(() => _resolverScope.ServiceProvider.GetService<ILogger<BackgroundJob>>(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <inheritdoc/>
        public IWriteableBackgroundJob ChangeQueue(string queue, QueuePriority priority)
        {
            queue.ValidateArgument(nameof(queue));

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

            return this;
        }

        #region Property
        /// <inheritdoc/>
        public T GetProperty<T>(string name)
        {
            name.ValidateArgument(nameof(name));

            if (TryGetProperty<T>(name, out var value))
            {
                return value;
            }

            throw new InvalidOperationException($"Job does not have a property with name <{name}>");
        }
        /// <inheritdoc/>
        public IWriteableBackgroundJob SetProperty<T>(string name, T value)
        {
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            var exists = _properties.Value.ContainsKey(name);

            if (!exists)
            {
                _properties.Value.Add(name, value);
            }
            else
            {
                _properties.Value[name] = value;
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

            return this;
        }
        /// <inheritdoc/>
        public bool TryGetProperty<T>(string name, out T value)
        {
            name.ValidateArgument(nameof(name));
            value = default;

            if (_properties.Value.ContainsKey(name))
            {
                value = _properties.Value[name].ConvertTo<T>();
                return true;
            }
            return false;
        }
        /// <inheritdoc/>
        public IWriteableBackgroundJob RemoveProperty(string name)
        {
            name.ValidateArgument(nameof(name));

            if (_properties.Value.ContainsKey(name))
            {
                _properties.Value.Remove(name);

                if (!ChangeLog.NewProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    ChangeLog.RemovedProperties.Add(name);
                    if (ChangeLog.UpdatedProperties.Contains(name, StringComparer.OrdinalIgnoreCase)) ChangeLog.UpdatedProperties.Remove(name);
                }

                return this;
            }
            else
            {
                throw new InvalidOperationException($"Job does not have a property with name <{name}>");
            }
        }
        #endregion

        #region Locking
        /// <inheritdoc/>
        public async Task<ILockedBackgroundJob> LockAsync(string requester = null, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {Environment} for {this} to lock job");

            await using (var storage = await StorageProvider.Value.GetStorageAsync(Environment, token).ConfigureAwait(false))
            {
                await using (var connection = await storage.Component.OpenConnectionAsync(true, token).ConfigureAwait(true))
                {
                    var job = await LockAsync(connection, requester, token).ConfigureAwait(false);

                    await connection.CommitAsync(token).ConfigureAwait(false);

                    return job;
                }
            }
        }
        /// <inheritdoc/>
        public async Task<ILockedBackgroundJob> LockAsync(IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot lock {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot lock a new background job");

            var hasLock = HasLock;
            Logger.Log($"Trying to acquire exclusive lock on {this}");
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                var lockState = await BackgroundJobService.Value.LockAsync(Id, connection, requester, token).ConfigureAwait(false);
                Set(lockState);
                HasLock = true;
            }

            Logger.Log($"{this} is now locked by <{Lock?.LockedBy}>");
            await StartKeepAliveTask(token).ConfigureAwait(false);

            // We didn't have lock before so refresh state as changes could have been made
            if (!hasLock)
            {
                await RefreshAsync(token).ConfigureAwait(false);
            }

            return this;
        }
        /// <inheritdoc/>
        public void OnStaleLock(Delegates.Async.AsyncAction<CancellationToken> action)
        {
            action.ValidateArgument(nameof(action));
            _staleLockActions ??= new List<Delegates.Async.AsyncAction<CancellationToken>>();
            _staleLockActions.Add(action);
        }

        private async Task StartKeepAliveTask(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            Logger.Debug($"Starting keep lock alive task for {this}");
            await TaskManager.Value.ScheduleActionAsync(this, HeartbeatTaskName, false, KeepLockAlive, x => x.WithPolicy(NamedManagedTaskPolicy.CancelAndStart).WithManagedOptions(ManagedTaskOptions.GracefulCancellation), token: token).ConfigureAwait(false);
        }

        private async Task KeepLockAlive(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            Logger.Debug($"Task started to keep lock on {this} alive");

            while (!token.IsCancellationRequested)
            {
                if (IsDisposed.HasValue) return;
                bool isStale = false;
                // Determine sleep time
                DateTime sleepTime;
                await using (await _lock.LockAsync(token))
                {
                    if (Lock == null)
                    {
                        Logger.Debug($"Lock is no longer set on {this}. Stopping task");
                        return;
                    }

                    sleepTime = Lock.LockHeartbeatUtc.Add(_options.LockHeartbeatOffset).ToLocalTime();
                }

                Logger.Debug($"Keep lock alive task for {this} will sleep until <{sleepTime}>");
                // Sleep until it is time to set heartbeat
                await Helper.Async.SleepUntil(sleepTime, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    Logger.Debug($"Keep alive task for {this} is cancelled. Stopping task");
                    return;
                }

                // Set heartbeat
                try
                {
                    await using (await _lock.LockAsync(token))
                    {
                        if (Lock == null)
                        {
                            Logger.Debug($"Lock is no longer set on {this}. Stopping task");
                            return;
                        }

                        Logger.Log($"Setting heartbeat for lock on {this}");
                        await SetHeartbeat(token).ConfigureAwait(false);
                    }
                }
                catch (BackgroundJobNotFoundException)
                {
                    isStale = true;
                }
                catch (BackgroundJobAlreadyLockedException)
                {
                    isStale = true;
                }
                catch (OperationCanceledException)
                {
                    Logger.Debug($"Keep alive task for {this} is cancelled. Stopping task");
                    return;
                }
                catch (Exception ex)
                {
                    var waitTime = _options.LockHeartbeatOffset / 3;
                    Logger.Log($"Keep alive task for {this} could not set heartbeat. Retrying in <{waitTime}>", ex);
                    await Helper.Async.Sleep(waitTime, token).ConfigureAwait(false);
                }

                if (isStale)
                {
                    await using (await _lock.LockAsync(token))
                    {
                        await DeclareLockStale(token).ConfigureAwait(false);
                    }
                }
            }

            Logger.Debug($"Keep alive task for {this} is cancelled. Stopping task");
        }

        private async Task DeclareLockStale(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            Logger.Warning($"Declaring lock on {this} is stale");
            Delegates.Async.AsyncAction<CancellationToken>[] actions = null;
            lock (_staleLockActions)
            {
                actions = _staleLockActions.ToArrayOrDefault();
            }

            HasLock = false;

            if (actions.HasValue())
            {
                foreach (var action in actions)
                {
                    try
                    {
                        await action(token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"On stale lock action could not be executed properly", ex);
                    }
                }
            }
        }

        private async Task ValidateLock(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            // Check if we have lock
            if (!HasLock || Lock == null) throw new BackgroundJobLockStaleException(Id, Environment);

            bool isStale = false;
            // Check if we are within the safety offset try to set the heartbeat
            if (Lock.LockHeartbeatUtc.Add(_options.LockTimeout) < DateTime.UtcNow.Add(-_options.LockExpirySafetyOffset))
            {
                Logger.Warning($"Lock on {this} is within the safety offset. Trying to extend lock");
                try
                {
                    await SetHeartbeat(token).ConfigureAwait(false);
                }
                catch (BackgroundJobNotFoundException)
                {
                    isStale = true;
                }
                catch (BackgroundJobAlreadyLockedException)
                {
                    isStale = true;
                }
            }

            if (isStale)
            {
                await DeclareLockStale(token).ConfigureAwait(false);
            }
        }

        private async Task SetHeartbeat(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            Logger.Debug($"Opening new connection to storage in environment {Environment} for {this} to set heartbeat on lock");
            await using (var storage = await StorageProvider.Value.GetStorageAsync(Environment, token).ConfigureAwait(false))
            {
                await using (var connection = await storage.Component.OpenConnectionAsync(true, token).ConfigureAwait(true))
                {
                    var lockState = await BackgroundJobService.Value.HeartbeatLockAsync(Id, Lock.LockedBy, connection, token).ConfigureAwait(false);

                    await connection.CommitAsync(token).ConfigureAwait(false);
                    Set(lockState);
                    HasLock = true;
                }
            }
        }
        #endregion

        #region Refresh
        /// <inheritdoc/>
        public async Task RefreshAsync(CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {Environment} for {this} to refresh job");

            await using (var storage = await StorageProvider.Value.GetStorageAsync(Environment, token).ConfigureAwait(false))
            {
                await using (var connection = await storage.Component.OpenConnectionAsync(true, token).ConfigureAwait(true))
                {
                    await RefreshAsync(connection, token).ConfigureAwait(false);

                    await connection.CommitAsync(token).ConfigureAwait(false);
                }
            }
        }
        /// <inheritdoc/>
        public async Task RefreshAsync(IStorageConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot refresh state for {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot refresh state on new background job");

            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                Logger.Log($"Refreshing state for {this}");

                var currentLockHolder = Lock?.LockedBy;

                var currentState = await BackgroundJobService.Value.GetAsync(Id, connection, token).ConfigureAwait(false);

                // Check if lock is still valid
                if (currentLockHolder != null && !currentLockHolder.EqualsNoCase(currentState?.Lock.LockedBy))
                {
                    await DeclareLockStale(token).ConfigureAwait(false);
                }
                Set(currentState);
                Logger.Log($"Refreshed state for {this}");
            }
        }

        private void Set(JobStorageData data)
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
            Set(data.States);
        }
        private void Set(LockStorageData data)
        {
            if (data == null)
            {
                Lock = null;
                return;
            }

            Lock = data;
        }
        private void Set(IEnumerable<StorageProperty> properties)
        {
            if (properties.HasValue())
            {
                _properties = new Lazy<Dictionary<string, object>>(() => properties.ToDictionary(x => x.Name, x => x.GetValue(_options, Cache.Value), StringComparer.OrdinalIgnoreCase), LazyThreadSafetyMode.ExecutionAndPublication);
            }
            else
            {
                _properties = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>(), true);
            }
        }
        private void Set(IEnumerable<MiddlewareStorageData> middleware)
        {
            if (middleware.HasValue())
            {
                _middleware = middleware.Select(x => new MiddlewareInfo(x.Type, x.Context, x.Priority)).ToList();
            }
        }
        private void Set(InvocationStorageData data)
        {
            data.ValidateArgument(nameof(data));

            _invocation = new Lazy<IInvocationInfo>(() => new InvocationInfo(data), true);
        }
        private void Set(IEnumerable<JobStateStorageData> data)
        {
            data.ValidateArgumentNotNullOrEmpty(nameof(data));

            _states = new Lazy<List<IBackgroundJobState>>(() => data.Select(x => BackgroundJobService.Value.ConvertToState(x, _options)).ToList(), LazyThreadSafetyMode.ExecutionAndPublication);
        }
        #endregion

        /// <inheritdoc/>
        public IWriteableBackgroundJob RegenerateExecutionId()
        {
            ExecutionId = Guid.NewGuid();
            ChangeLog.ExecutionIdChanged = true;
            return this;
        }

        #region State management
        /// <inheritdoc/>
        public async Task<bool> ChangeStateAsync(IBackgroundJobState state, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            state.ValidateArgument(nameof(state));
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                Logger.Log($"Starting state election for {this} to transition into state <{state}>");

                bool elected = false;
                bool originalElected = true;
                do
                {
                    // Set state
                    Logger.Debug($"Applying state <{state}> on {this}");
                    ChangeLog.NewStates.Add(state);
                    await ApplyStateAsync(state, token).ConfigureAwait(false);

                    // Try and elect state as final
                    Logger.Debug($"Trying to elect state <{state}> on {this} as final");
                    var result = await Notifier.Value.RequestAsync<BackgroundJobStateElectionRequest, IBackgroundJobState>(this, new BackgroundJobStateElectionRequest(this, State), token).ConfigureAwait(false);

                    if (result.Completed)
                    {
                        originalElected = false;
                        state = result.Response;
                        Logger.Debug($"State election resulted in new state <{state}> for {this}");
                    }
                    else
                    {
                        elected = true;
                    }
                }
                while (!elected);


                Logger.Log($"Final state <{state}> elected for {this}");
                return originalElected;
            }
        }

        private async Task ApplyStateAsync(IBackgroundJobState state, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            state.ValidateArgument(nameof(state));

            SetState(state);

            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobStateAppliedEvent(this), token).ConfigureAwait(false);
        }

        private void SetState(IBackgroundJobState state)
        {
            state.ValidateArgument(nameof(state));

            _states ??= new Lazy<List<IBackgroundJobState>>(new List<IBackgroundJobState>());
            _states.Value.Add(state);

            state.ElectedDateUtc = DateTime.UtcNow;
        }
        #endregion

        #region Persistance
        /// <inheritdoc/>
        public async Task SaveChangesAsync(IStorageConnection connection, bool retainLock, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot save changes to {this} in environment {Environment} with storage connection to environment {connection.Environment}");

            Logger.Log($"Saving changes made to {this}");
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                // Validate lock
                if (Id.HasValue()) await ValidateLock(token).ConfigureAwait(false);

                ModifiedAtUtc = DateTime.UtcNow;
                await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavingEvent(this, connection, IsCreation), token).ConfigureAwait(false);

                if (!retainLock) HasLock = false;

                var storageFormat = await BackgroundJobService.Value.ConvertToStorageFormatAsync(this, token).ConfigureAwait(false);
                var id = await BackgroundJobService.Value.StoreAsync(connection, storageFormat, !retainLock, token).ConfigureAwait(false);
                Id = id;

                if (!retainLock) Lock = null;

                if (connection.HasTransaction)
                {
                    // Register delegate to raise event if the current transaction is being commited
                    connection.OnCommitting(async x => await RaiseOnPersistedAsync(connection, x).ConfigureAwait(false));
                }
                else
                {
                    await RaiseOnPersistedAsync(connection, token).ConfigureAwait(false);
                }
            }
        }
        /// <inheritdoc/>
        public async Task SaveChangesAsync(bool retainLock, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {Environment} for {this} to save changes");

            await using (var storage = await StorageProvider.Value.GetStorageAsync(Environment, token).ConfigureAwait(false))
            {
                await using (var connection = await storage.Component.OpenConnectionAsync(true, token).ConfigureAwait(true))
                {
                    await SaveChangesAsync(connection, retainLock, token).ConfigureAwait(false);

                    await connection.CommitAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task RaiseOnPersistedAsync(IStorageConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));

            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavedEvent(this, connection, IsCreation), x => x.Enlist(new BackgroundJobFinalStateElectedEvent(this, connection))
                                                                                                                        .WithOptions(EventOptions.AllowParallelExecution)
                                                                                  , token).ConfigureAwait(false);
        }
        #endregion

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            if (HasLock)
            {
                Logger.Debug($"Opening new connection to storage in environment {Environment} for {this} to release job");

                await using (var storage = await StorageProvider.Value.GetStorageAsync(Environment).ConfigureAwait(false))
                {
                    await using (var connection = await storage.Component.OpenConnectionAsync(true).ConfigureAwait(true))
                    {
                        await ReleaseAsync(connection).ConfigureAwait(false);

                        await connection.CommitAsync().ConfigureAwait(false);
                    }
                }
            }
            else
            {
                await ReleaseAsync().ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async ValueTask DisposeAsync(IStorageConnection storageConnection)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            if (IsDisposed.HasValue) return;

            await ReleaseAsync(storageConnection).ConfigureAwait(false);
        }

        private async Task ReleaseAsync(IStorageConnection storageConnection = null)
        {
            if (IsDisposed.HasValue) return;
            if (storageConnection != null && !storageConnection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot release {this} in environment {Environment} with storage connection to environment {storageConnection.Environment}");


            Logger.Log($"Disposing {this}");
            await using (await _lock.LockAsync().ConfigureAwait(false))
            {
                if (IsDisposed.HasValue) return;
                using (new ExecutedAction(x => IsDisposed = x))
                {
                    var exceptions = new List<Exception>();
                    Logger.Log($"Stopping tasks tied to {this}");
                    // Stop tasks
                    try
                    {
                        if (TaskManager.IsValueCreated) await TaskManager.Value.StopAllForAsync(this);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }

                    // Release lock
                    if (storageConnection != null)
                    {
                        try
                        {
                            if (Lock != null)
                            {
                                Logger.Debug($"Releasing lock on {this}");

                                await storageConnection.Storage.UnlockBackgroundJobAsync(Id, Lock.LockedBy, storageConnection).ConfigureAwait(false);

                                Logger.Log($"Released lock on {this}");
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        } 
                    }

                    // Release services
                    try
                    {
                        await _resolverScope.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }

                    if (exceptions.HasValue()) throw new AggregateException("Could not properly dispose background job", exceptions);
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Id.HasValue() ? $"Background job {Id}" : "New background job";
        }
    }
}
