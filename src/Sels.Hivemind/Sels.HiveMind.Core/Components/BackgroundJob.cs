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
using Sels.HiveMind.Models.Queue;
using Sels.HiveMind.Service.Job;
using Sels.HiveMind.Requests.Job;

namespace Sels.HiveMind
{
    /// <summary>
    /// Manages state of a background job.
    /// </summary>
    public class BackgroundJob : ILockedBackgroundJob, IAsyncExposedDisposable
    {
        // Fields
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly AsyncServiceScope _resolverScope;
        private readonly List<IBackgroundJobState> _stateHistory;
        private readonly Lazy<Dictionary<string, object>> _properties;
        private readonly List<MiddlewareInfo> _middleware;

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
        public IBackgroundJobState State { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyList<IBackgroundJobState> StateHistory => _stateHistory;
        /// <inheritdoc/>
        public ILockInfo Lock { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties => _properties.Value;
        /// <inheritdoc/>
        public IInvocationInfo Invocation { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyList<IMiddlewareInfo> Middleware => _middleware;

        // Services
        private Lazy<IStorageProvider> StorageProvider { get; }
        private Lazy<IBackgroundJobService> BackgroundJobService { get; }
        private Lazy<INotifier> Notifier { get; }
        private Lazy<ILogger> LazyLogger { get; }
        private ILogger Logger => LazyLogger.Value;

        // State
        private bool IsCreation { get; }
        /// <inheritdoc/>
        public bool NeedsDispose => Helper.Collection.Enumerate(StorageProvider.IsValueCreated, BackgroundJobService.IsValueCreated, Notifier.IsValueCreated, LazyLogger.IsValueCreated).Any(x => x == true);
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
        public BackgroundJob(AsyncServiceScope resolverScope, string environment, string queue, QueuePriority priority, IInvocationInfo invocationInfo, IReadOnlyDictionary<string, object> properties, IEnumerable<MiddlewareInfo> middleware) : this()
        {
            _resolverScope = resolverScope;
            IsCreation = true;
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Queue = queue.ValidateArgument(nameof(queue));
            Priority = priority;
            Invocation = invocationInfo.ValidateArgument(nameof(invocationInfo));
            _properties = new Lazy<Dictionary<string, object>>(() => properties.HasValue() ? properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), true);
            _middleware = middleware != null ? middleware.ToList() : new List<MiddlewareInfo>();
            _stateHistory = new List<IBackgroundJobState>();

            SetState(new CreatedState());
            RegenerateExecutionId();
            CreatedAtUtc = DateTime.UtcNow;
            ModifiedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new instance from a persisted job.
        /// </summary>
        /// <param name="resolverScope">Scope used to resolve services scoped to the lifetime of the job instance</param>
        /// <param name="environment">The environment <paramref name="storageData"/> was retrieved from</param>
        /// <param name="storageData">The persisted state of the job</param>
        public BackgroundJob(AsyncServiceScope resolverScope, string environment, JobStateStorageData storageData) : this()
        {
            _resolverScope = resolverScope;
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }

        private BackgroundJob()
        {
            StorageProvider = new Lazy<IStorageProvider>(() => _resolverScope.ServiceProvider.GetRequiredService<IStorageProvider>(), true);
            BackgroundJobService = new Lazy<IBackgroundJobService>(() => _resolverScope.ServiceProvider.GetRequiredService<IBackgroundJobService>(), true);
            Notifier = new Lazy<INotifier>(() => _resolverScope.ServiceProvider.GetRequiredService<INotifier>(), true);
            LazyLogger = new Lazy<ILogger>(() => _resolverScope.ServiceProvider.GetService<ILogger<BackgroundJob>>(), true);
        }

        /// <inheritdoc/>
        public IWriteableBackgroundJob ChangeQueue(string queue, QueuePriority priority)
        {
            queue.ValidateArgument(nameof(queue));

            if(!queue.Equals(Queue, StringComparison.OrdinalIgnoreCase))
            {
                Queue = queue;
                if(!ChangeLog.QueueChanged) ChangeLog.QueueChanged = true;
            }
            if(Priority != priority)
            {
                Priority = priority;
                if(!ChangeLog.PriorityChanged) ChangeLog.PriorityChanged = true;
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

            if (exists)
            {
                _properties.Value.Add(name, value);
            }
            else
            {
                _properties.Value[name] = value;
            }

            if (exists && !ChangeLog.NewProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                ChangeLog.UpdatedProperties.Add(name);
            }
            else if (ChangeLog.RemovedProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                ChangeLog.UpdatedProperties.Add(name);
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
                }

                return this;
            }
            else
            {
                throw new InvalidOperationException($"Job does not have a property with name <{name}>");
            }
        }
        #endregion

        /// <inheritdoc/>
        public Task<ILockedBackgroundJob> LockAsync(string requester, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task RefreshAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
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
                    var result = await Notifier.Value.RequestAsync<BackgroundJobStateElectionRequest, IBackgroundJobState>(this, new BackgroundJobStateElectionRequest(this, _stateHistory.Last()), token).ConfigureAwait(false);

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
            state.ValidateArgument(nameof(state));

            SetState(state);

            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobStateAppliedEvent(this), token).ConfigureAwait(false);
        }

        private void SetState(IBackgroundJobState state)
        {
            state.ValidateArgument(nameof(state));

            if (State != null) _stateHistory.Add(State);
            State = state;
        }
        #endregion

        #region Persistance
        /// <inheritdoc/>
        public async Task SaveChangesAsync(IStorageConnection connection, bool retainLock, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.Equals(Environment, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Cannot save changes to {this} in environment {Environment} with storage connection to environment {connection.Environment}");

            Logger.Log($"Saving changes made to {this}");
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                ModifiedAtUtc = DateTime.UtcNow;
                await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavingEvent(this, connection, IsCreation), token).ConfigureAwait(false);

                if (!retainLock) Lock = null;
                var storageFormat = await BackgroundJobService.Value.ConvertToStorageFormatAsync(this, token).ConfigureAwait(false);
                var id = await BackgroundJobService.Value.StoreAsync(connection, storageFormat, token).ConfigureAwait(false);
                Id = id;

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

            Logger.Log($"Disposing {this}");
            await using (await _lock.LockAsync().ConfigureAwait(false))
            {
                using (new ExecutedAction(x => IsDisposed = x))
                {
                    var exceptions = new List<Exception>();

                    try
                    {

                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                    finally
                    {
                        try
                        {
                            await _resolverScope.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
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
