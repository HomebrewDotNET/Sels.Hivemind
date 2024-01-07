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
using Sels.HiveMind.Requests.Job;
using Sels.Core.Extensions.Text;
using Sels.Core.Async.TaskManagement;
using Newtonsoft.Json.Linq;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Client;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using static Sels.HiveMind.HiveMindConstants;
using Sels.Core.Extensions.Linq;
using Sels.HiveMind.Service;

namespace Sels.HiveMind
{
    /// <summary>
    /// Manages state of a background job.
    /// </summary>
    public class BackgroundJob : ILockedBackgroundJob, IAsyncExposedDisposable
    {
        // Fields
        private readonly object _lock = new object();
        private readonly AsyncServiceScope _resolverScope;
        private LazyPropertyInfoDictionary _properties;
        private LockStorageData _lockData;
        private InvocationInfo _invocation;
        private List<MiddlewareInfo> _middleware;
        private readonly HiveMindOptions _options;
        private List<BackgroundJobStateInfo> _states;

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
        public IBackgroundJobState State => _states?.FirstOrDefault()?.State;
        /// <inheritdoc/>
        public IEnumerable<IBackgroundJobState> StateHistory => _states?.Skip(1).Select(x => x.State);
        /// <inheritdoc/>
        public ILockInfo Lock => _lockData;
        /// <inheritdoc/>
        public bool HasLock { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties => _properties;
        private IDictionary<string, object> WriteableProperties => _properties;
        /// <inheritdoc/>
        public IInvocationInfo Invocation => _invocation;
        /// <inheritdoc/>
        public IReadOnlyList<IMiddlewareInfo> Middleware => _middleware;
        /// <inheritdoc/>
        public bool IsDeleted { get; set; }
        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public JobStorageData StorageData
        {
            get
            {
                var jobStorage = new JobStorageData(this, _invocation.StorageData, _lockData, _properties.Properties.Select(x => x.StorageData), _middleware.Select(x => x.StorageData), _options, Cache.Value);

                var states = new List<BackgroundJobStateInfo>(_states);
                states.Reverse();
                foreach(var state in states)
                {
                    jobStorage.AddState(state.StorageData, state.IsInitialized && ChangeLog.NewStates.Contains(state.State));
                }

                return jobStorage;
            }
        }

        // Services
        private Lazy<IBackgroundJobClient> Client { get; }
        private Lazy<IBackgroundJobService> BackgroundJobService { get; }
        private Lazy<INotifier> Notifier { get; }
        private Lazy<IMemoryCache> Cache { get; }
        private Lazy<ILogger> LazyLogger { get; }
        private ILogger Logger => LazyLogger.Value;

        // State
        private bool IsCreation { get; }
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
        public BackgroundJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment, string queue, QueuePriority priority, InvocationInfo invocationInfo, IReadOnlyDictionary<string, object> properties, IEnumerable<MiddlewareInfo> middleware) : this()
        {
            _options = options.ValidateArgument(nameof(options));

            _resolverScope = resolverScope;
            IsCreation = true;
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Queue = queue.ValidateArgument(nameof(queue));
            Priority = priority;
            _invocation = invocationInfo.ValidateArgument(nameof(invocationInfo));
            _properties = new LazyPropertyInfoDictionary(_options, Cache.Value);
            properties.Execute(x => SetProperty(x.Key, x.Value));
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
        public BackgroundJob(IClientConnection connection, AsyncServiceScope resolverScope, HiveMindOptions options, string environment, JobStorageData storageData, bool hasLock) : this()
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
            }
        }

        private BackgroundJob()
        {
            Client = new Lazy<IBackgroundJobClient>(() => _resolverScope.ServiceProvider.GetRequiredService<IBackgroundJobClient>(), LazyThreadSafetyMode.ExecutionAndPublication);
            BackgroundJobService = new Lazy<IBackgroundJobService>(() => _resolverScope.ServiceProvider.GetRequiredService<IBackgroundJobService>(), LazyThreadSafetyMode.ExecutionAndPublication);
            Notifier = new Lazy<INotifier>(() => _resolverScope.ServiceProvider.GetRequiredService<INotifier>(), LazyThreadSafetyMode.ExecutionAndPublication);
            Cache = new Lazy<IMemoryCache>(() => _resolverScope.ServiceProvider.GetRequiredService<IMemoryCache>(), LazyThreadSafetyMode.ExecutionAndPublication);
            LazyLogger = new Lazy<ILogger>(() => _resolverScope.ServiceProvider.GetService<ILogger<BackgroundJob>>(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <inheritdoc/>
        public IWriteableBackgroundJob ChangeQueue(string queue, QueuePriority priority)
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

            return this;
        }

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
        public IWriteableBackgroundJob SetProperty<T>(string name, T value)
        {
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            lock (_lock)
            {
                var exists = WriteableProperties.ContainsKey(name);

                if (!exists)
                {
                    WriteableProperties.Add(name, value);
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

            return this;
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
        public IWriteableBackgroundJob RemoveProperty(string name)
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

                    return this;
                }
                else
                {
                    throw new InvalidOperationException($"Job does not have a property with name <{name}>");
                }
            }
        }
        #endregion

        #region Locking
        /// <inheritdoc/>
        public async Task<ILockedBackgroundJob> LockAsync(string requester = null, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for {HiveLog.Job.Id} to lock job", Environment, Id);

            await using (var connection = await Client.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                var job = await LockAsync(connection, requester, token).ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);

                return job;
            }
        }
        /// <inheritdoc/>
        public async Task<ILockedBackgroundJob> LockAsync(IClientConnection connection, string requester = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot lock {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot lock a new background job");
            if (IsDeleted) throw new InvalidOperationException($"Cannot lock deleted background job");

            var hasLock = HasLock;
            if (hasLock) hasLock = await MaintainLock(token).ConfigureAwait(false);

            if (!hasLock)
            {
                Logger.Log($"Trying to acquire exclusive lock on background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> for <{(requester ?? "RANDOM")}>", Id, Environment);

                var lockState = await BackgroundJobService.Value.LockAsync(Id, connection.StorageConnection, requester, token).ConfigureAwait(false);

                lock (_lock)
                {
                    Set(lockState);
                    HasLock = true;
                }

                Logger.Log($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
            }
            else
            {
                Logger.Log(HasLock ? LogLevel.Information : LogLevel.Warning, $"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> already locked by <{HiveLog.Job.LockHolder}>", Id, Environment, Lock?.LockedBy);
            }

            // We didn't have lock before so refresh state as changes could have been made
            if (!hasLock)
            {
                await RefreshAsync(connection, token).ConfigureAwait(false);
            }

            return this;
        }
        /// <inheritdoc/>
        public async Task<bool> SetHeartbeatAsync(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            if (IsDeleted) return false;
            lock (_lock)
            {
                if (!HasLock) return false;
            }

            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for background job <{HiveLog.Job.Id}> to set heartbeat on lock", Environment, Id);
            try
            {
                LockStorageData lockState = null;
                await using (var connection = await Client.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
                {
                    Logger.Debug($"Updating heartbeat in storage for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
                    lockState = await BackgroundJobService.Value.HeartbeatLockAsync(Id, Lock.LockedBy, connection.StorageConnection, token).ConfigureAwait(false);
                    await connection.CommitAsync(token).ConfigureAwait(false);

                    Logger.Debug($"Heartbeat in storage for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> has been set to <{lockState.LockHeartbeatUtc.ToLocalTime()}>");
                }

                lock (_lock)
                {
                    Set(lockState);
                    HasLock = true;
                }
                return true;
            }
            catch (BackgroundJobAlreadyLockedException)
            {
                lock (_lock)
                {
                    HasLock = false;
                }

                return false;
            }
        }
        private async Task ValidateLock(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            
            if(!await MaintainLock(token).ConfigureAwait(false))
            {
                throw new BackgroundJobLockStaleException(Id, Environment);
            }
        }
        private async Task<bool> MaintainLock(CancellationToken token)
        {
            using var methodLogger = Logger.TraceMethod(this);
            bool inSafetyOffset = false;
            lock (_lock)
            {
                // Check if we have lock
                if (!HasLock || Lock == null) return false;
                // Check if we are within the safety offset try to set the heartbeat
                if (Lock.LockHeartbeatUtc.Add(_options.LockTimeout) < DateTime.UtcNow.Add(-_options.LockExpirySafetyOffset))
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
                catch (BackgroundJobNotFoundException)
                {
                    isStale = true;
                }
            }

            if (isStale)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Refresh
        /// <inheritdoc/>
        public async Task RefreshAsync(CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for background job <{HiveLog.Job.Id}> to refresh job", Environment, Id);

            await using (var connection = await Client.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                await RefreshAsync(connection, token).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async Task RefreshAsync(IClientConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot refresh state for {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot refresh state on new background job");
            if (IsDeleted) throw new InvalidOperationException($"Cannot refresh deleted background job");

            Logger.Log($"Refreshing state for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            var currentLockHolder = Lock?.LockedBy;

            var currentState = await BackgroundJobService.Value.GetAsync(Id, connection.StorageConnection, token).ConfigureAwait(false);

            // Check if lock is still valid
            if (currentLockHolder != null && !currentLockHolder.EqualsNoCase(currentState?.Lock?.LockedBy))
            {
                lock (_lock)
                {
                    HasLock = false;
                }
            }

            lock (_lock)
            {
                Set(currentState);
            }

            Logger.Log($"Refreshed state for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
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
                _lockData = null;
                return;
            }

            _lockData = data;
        }
        private void Set(IEnumerable<StorageProperty> properties)
        {
            if(properties != null)
            {
                _properties = new LazyPropertyInfoDictionary(properties.Select(x => new LazyPropertyInfo(x, _options, Cache.Value)), _options, Cache.Value);
            }
            else
            {
                _properties = new LazyPropertyInfoDictionary(_options, Cache.Value);
            }
        }
        private void Set(IEnumerable<MiddlewareStorageData> middleware)
        {
            _middleware = middleware != null ? middleware.Select(x => new MiddlewareInfo(x, _options, Cache.Value)).ToList() : new List<MiddlewareInfo>();
        }
        private void Set(InvocationStorageData data)
        {
            data.ValidateArgument(nameof(data));

            _invocation = new InvocationInfo(data, _options, Cache.Value);
        }
        private void Set(IEnumerable<JobStateStorageData> data)
        {
            data.ValidateArgumentNotNullOrEmpty(nameof(data));

            _states = new List<BackgroundJobStateInfo>(data.Select(x => new BackgroundJobStateInfo(x, BackgroundJobService, _options)));
            _states.Reverse();
        }
        #endregion

        /// <summary>
        /// Signals the job that it no longer holds the lock.
        /// </summary>
        public void RemoveHoldOnLock()
        {
            lock (_lock)
            {
                HasLock = false;
            }
        }

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
            if (IsDeleted) throw new InvalidOperationException($"Cannot change state on deleted background job");
            state.ValidateArgument(nameof(state));
            Logger.Log($"Starting state election for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to transition into state <{HiveLog.BackgroundJob.State}>", Id, Environment, state.Name);

            bool elected = false;
            bool originalElected = true;
            do
            {
                // Set state
                Logger.Debug($"Applying state <{HiveLog.BackgroundJob.State}> on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.Name, Id, Environment);
                await ApplyStateAsync(state, token).ConfigureAwait(false);

                // Try and elect state as final
                Logger.Debug($"Trying to elect state <{HiveLog.BackgroundJob.State}> on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> as final", state.Name, Id, Environment);
                var result = await Notifier.Value.RequestAsync<BackgroundJobStateElectionRequest, IBackgroundJobState>(this, new BackgroundJobStateElectionRequest(this, State), token).ConfigureAwait(false);

                if (result.Completed)
                {
                    originalElected = false;
                    state = result.Response;
                    Logger.Debug($"State election resulted in new state <{HiveLog.BackgroundJob.State}> for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.Name, Id, Environment);
                }
                else
                {
                    elected = true;
                }
            }
            while (!elected);


            Logger.Log($"Final state <{HiveLog.BackgroundJob.State}> elected for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.Name, Id, Environment);
            return originalElected;
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

            lock (_lock)
            {
                _states ??= new List<BackgroundJobStateInfo>();
                _states.Insert(0, new BackgroundJobStateInfo(state, BackgroundJobService, _options));
                ChangeLog.NewStates.Add(state);
                state.ElectedDateUtc = DateTime.UtcNow; 
            }
        }
        #endregion

        #region Persistance
        /// <inheritdoc/>
        public async Task SaveChangesAsync(IClientConnection connection, bool retainLock, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot save changes to {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (IsDeleted) throw new InvalidOperationException($"Cannot save changes to deleted background job");

            Logger.Log($"Saving changes made to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
            // Validate lock
            if (Id.HasValue()) await ValidateLock(token).ConfigureAwait(false);

            lock (_lock)
            {
                ModifiedAtUtc = DateTime.UtcNow; 
            }
            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavingEvent(this, connection, IsCreation), token).ConfigureAwait(false);

            lock (_lock)
            {
                if (!retainLock) HasLock = false; 
            }

            var storageFormat = StorageData;
            var id = await BackgroundJobService.Value.StoreAsync(connection.StorageConnection, storageFormat, !retainLock, token).ConfigureAwait(false);
            lock (_lock)
            {
                Id = id;
                if (!retainLock) _lockData = null; 
            }

            if (connection.HasTransaction)
            {
                // Register delegate to raise event if the current transaction is being commited
                connection.StorageConnection.OnCommitting(async x => await RaiseOnPersistedAsync(connection, x).ConfigureAwait(false));
            }
            else
            {
                await RaiseOnPersistedAsync(connection, token).ConfigureAwait(false);
            }

            Logger.Log($"Saved changes made to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
        }
        /// <inheritdoc/>
        public async Task SaveChangesAsync(bool retainLock, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for background job <{HiveLog.Job.Id}> to save changes", Environment, Id);

            await using (var connection = await Client.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                await SaveChangesAsync(connection, retainLock, token).ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
            }
        }

        private async Task RaiseOnPersistedAsync(IClientConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));

            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavedEvent(this, connection, IsCreation), x => x.Enlist(new BackgroundJobFinalStateElectedEvent(this, connection))
                                                                                  , token).ConfigureAwait(false);
        }
        #endregion

        #region Deletion
        public async Task SystemDeleteAsync(IClientConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot delete {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            if (!Id.HasValue()) throw new InvalidOperationException($"Cannot delete new background job");
            if (IsDeleted) throw new InvalidOperationException($"Cannot delete an already deleted background job");

            Logger.Log($"Permanently deleting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            await ValidateLock(token).ConfigureAwait(false);
            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobDeletingEvent(this, connection), token).ConfigureAwait(false);

            // Delete
            if(!await connection.StorageConnection.Storage.TryDeleteBackgroundJobAsync(Id, Lock?.LockedBy, connection.StorageConnection, token).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Could not delete delete {this} in environment {Environment}");
            }

            lock (_lock)
            {
                IsDeleted = true;
            }

            if (connection.HasTransaction)
            {
                // Register delegate to raise event if the current transaction is being commited
                connection.StorageConnection.OnCommitting(async x => await RaiseOnDeletedAsync(connection, x).ConfigureAwait(false));
            }
            else
            {
                await RaiseOnDeletedAsync(connection, token).ConfigureAwait(false);
            }

            Logger.Log($"Permanently deleted background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
        }

        public async Task SystemDeleteAsync(CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.Environment}> for background job <{HiveLog.Job.Id}> to delete job", Environment, Id);

            await using (var connection = await Client.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                await SystemDeleteAsync(connection, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
            }
        }

        private async Task RaiseOnDeletedAsync(IClientConnection connection, CancellationToken token = default)
        {
            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobDeletedEvent(this, connection), token).ConfigureAwait(false);
        }
        #endregion

        #region Data
        /// <inheritdoc/>
        public async Task<(bool Exists, T Data)> TryGetDataAsync<T>(IClientConnection connection, string name, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot fetch data <{name}> from {this} in environment {Environment} with storage connection to environment {connection.Environment}");

            Logger.Log($"Trying to fetch data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            return await BackgroundJobService.Value.TryGetDataAsync<T>(connection.StorageConnection, Id, name, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task<(bool Exists, T Data)> TryGetDataAsync<T>(string name, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for background job {HiveLog.Job.Id} to fetch data <{name}>", Environment, Id);

            await using (var connection = await Client.Value.OpenConnectionAsync(Environment, false, token).ConfigureAwait(false))
            {
                return await TryGetDataAsync<T>(connection, name, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task SetDataAsync<T>(IClientConnection connection, string name, T value, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot fetch data <{name}> from {this} in environment {Environment} with storage connection to environment {connection.Environment}");

            Logger.Log($"Saving data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

            await BackgroundJobService.Value.SetDataAsync<T>(connection.StorageConnection, Id, name, value, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task SetDataAsync<T>(string name, T value, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            Logger.Debug($"Opening new connection to storage in environment {HiveLog.Environment} for background job {HiveLog.Job.Id} to save data <{name}>", Environment, Id);

            await using (var connection = await Client.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                await SetDataAsync<T>(connection, name, value, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
            }
        }
        #endregion

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            Logger.Debug($"Disposing background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
           
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
                    if (HasLock && !IsDeleted)
                    {
                        Logger.Debug($"Releasing lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);

                        await using (var connection = await Client.Value.OpenConnectionAsync(Environment, true).ConfigureAwait(false))
                        {
                            await connection.StorageConnection.Storage.UnlockBackgroundJobAsync(Id, Lock.LockedBy, connection.StorageConnection).ConfigureAwait(false);

                            await connection.CommitAsync().ConfigureAwait(false);
                        }

                        Logger.Log($"Released lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                // Release services
                try
                {
                    Logger.Debug($"Disposing scope for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", Id, Environment);
                    await _resolverScope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                if (exceptions.HasValue()) throw new AggregateException("Could not properly dispose background job", exceptions);
            }
            finally
            {
                IsDisposed = true;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Id.HasValue() ? $"Background job {Id}" : "New background job";
        }       
    }
}
