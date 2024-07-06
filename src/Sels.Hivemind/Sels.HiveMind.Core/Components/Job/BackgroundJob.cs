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
using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Client;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using static Sels.HiveMind.HiveMindConstants;
using Sels.Core.Extensions.Linq;
using Sels.HiveMind.Service;
using static Sels.HiveMind.HiveLog;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Equality;
using Sels.HiveMind.Job.Actions;
using Sels.HiveMind.Templates.Job;
using Sels.Core.Mediator.Request;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Manages state of a background job.
    /// </summary>
    public class BackgroundJob : BaseJob<IBackgroundJobClient, IBackgroundJobService, ILockedBackgroundJob, BackgroundJobChangeLog, IBackgroundJobChangeTracker, BackgroundJobStorageData, IBackgroundJobState, JobStateStorageData, IBackgroundJobAction>, ILockedBackgroundJob
    {        
        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public override BackgroundJobStorageData StorageData
        {
            get
            {
                var jobStorage = new BackgroundJobStorageData(this, _invocation.StorageData, _lockData, _properties.Properties.Select(x => x.StorageData), _middleware.Select(x => x.StorageData), _options, Cache.Value);

                var states = new List<JobStateInfo<BackgroundJobStorageData, IBackgroundJobState, JobStateStorageData>>(_states);
                states.Reverse();
                foreach (var state in states)
                {
                    jobStorage.AddState(state.StorageData, state.IsInitialized && ChangeLog.NewStates.Contains(state.State));
                }

                return jobStorage;
            }
        }

        // Services
        /// <summary>
        /// Contains the changes made to the job.
        /// </summary>
        protected override BackgroundJobChangeLog ChangeLog { get; set; } = new BackgroundJobChangeLog();
        /// <inheritdoc/>
        protected override ILockedBackgroundJob Job => this;


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
        public BackgroundJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment, string queue, QueuePriority priority, InvocationInfo invocationInfo, IReadOnlyDictionary<string, object> properties, IEnumerable<MiddlewareInfo> middleware) : base(resolverScope, options, environment)
        {
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
        /// <param name="options"><inheritdoc cref="BaseJob{TState, TAction}._options"/></param>
        /// <param name="resolverScope"><inheritdoc cref="BaseJob{TState, TAction}._resolverScope"/></param>
        /// <param name="environment">The environment <paramref name="storageData"/> was retrieved from</param>
        /// <param name="storageData">The persisted state of the job</param>
        /// <param name="hasLock">True if the job was fetched with a lock, otherwise false for only reading the job</param>
        public BackgroundJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment, BackgroundJobStorageData storageData, bool hasLock) : base(resolverScope, options, environment, storageData, hasLock)
        {
        }


        #region Locking
        /// <inheritdoc/>
        protected override Task TryUnlockJobAsync(IStorageConnection connection) => connection.ValidateArgument(nameof(connection)).Storage.UnlockBackgroundJobAsync(Id, Lock?.LockedBy, connection);
        #endregion

        #region Refresh
        /// <inheritdoc/>
        protected override void Set(BackgroundJobStorageData data)
        {
            data.ValidateArgument(nameof(data));

            base.Set(data);
            
            Set(data.States);
        }
        /// <inheritdoc/>
        private void Set(IEnumerable<JobStateStorageData> data)
        {
            data.ValidateArgumentNotNullOrEmpty(nameof(data));

            _states = new List<JobStateInfo<BackgroundJobStorageData, IBackgroundJobState, JobStateStorageData>>(data.Select(x => new JobStateInfo<BackgroundJobStorageData, IBackgroundJobState, JobStateStorageData>(x, Environment, _options, Cache.Value)));
            _states.Reverse();
        }
        #endregion

        #region State management

        /// <inheritdoc/>
        protected async override Task<RequestResponse<IBackgroundJobState>> RaiseStateElectionRequest(IStorageConnection storageConnection, IBackgroundJobState state, CancellationToken token = default)
        {
            return await Notifier.Value.RequestAsync(this, new BackgroundJobStateElectionRequest(this, State, storageConnection), token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected async override Task RaiseStateAppliedEvent(IStorageConnection storageConnection, IBackgroundJobState state, CancellationToken token = default)
        {
            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobStateAppliedEvent(this, storageConnection), token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override async Task RaiseFinalStateElectedEvent(IStorageConnection storageConnection, IBackgroundJobState state, CancellationToken token = default)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            state.ValidateArgument(nameof(state));

            if (storageConnection.HasTransaction)
            {
                storageConnection.OnCommitted(t => Notifier.Value.RaiseEventAsync(this, new BackgroundJobFinalStateElectedEvent(this, storageConnection), t));
            }
            else
            {
                await Notifier.Value.RaiseEventAsync(this, new BackgroundJobFinalStateElectedEvent(this, storageConnection), token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Persistance
        /// <inheritdoc/>
        public async Task SaveChangesAsync(IStorageConnection connection, bool retainLock, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot save changes to {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            await using var lockScope = await _actionLock.LockAsync(token);

            lock (_lock)
            {
                if (IsDeleted) throw new InvalidOperationException($"Cannot save changes to deleted background job");
            }


            Logger.Log($"Saving changes made to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);
            // Validate lock
            if (Id.HasValue()) await ValidateLock(token).ConfigureAwait(false);

            lock (_lock)
            {
                ModifiedAtUtc = DateTime.UtcNow;
            }
            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavingEvent(this, connection, IsCreation), token).ConfigureAwait(false);

            lock (_lock)
            {
                if (!retainLock) _hasLock = false;
            }

            var storageFormat = StorageData;
            var id = await JobService.Value.StoreAsync(connection, storageFormat, !retainLock, token).ConfigureAwait(false);
            lock (_lock)
            {
                Id = id;
                if (!retainLock) _lockData = null;
                IsCommiting = true;
            }

            if (connection.HasTransaction)
            {
                // Register delegate to raise event if the current transaction is being commited
                connection.OnCommitting(async x => await RaiseOnPersistedAsync(connection, x).ConfigureAwait(false));
            }
            else
            {
                await RaiseOnPersistedAsync(connection, token).ConfigureAwait(false);
            }

            Logger.Log($"Saved changes made to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);
        }
        /// <inheritdoc/>
        public async Task SaveChangesAsync(bool retainLock, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.EnvironmentParam}> for background job <{HiveLog.Job.IdParam}> to save changes", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                await SaveChangesAsync(connection.StorageConnection, retainLock, token).ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
            }
        }

        private async Task RaiseOnPersistedAsync(IStorageConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));

            lock (_lock)
            {
                if (!IsCommiting) return;
                if (IsDisposed.HasValue) throw new ObjectDisposedException(nameof(BackgroundJob));
            }

            await Notifier.Value.RaiseEventAsync(this, new BackgroundJobSavedEvent(this, connection, IsCreation), x => x.Enlist(new BackgroundJobFinalStateElectedEvent(this, connection))
                                                                                  , token).ConfigureAwait(false);

            lock (_lock)
            {
                IsCommiting = false;
                // Reset change log
                ChangeLog = new BackgroundJobChangeLog();
            }
        }
        #endregion

        #region Deletion      
        /// <inheritdoc/>
        protected override IBackgroundJobState CreateSystemDeletingState() => new SystemDeletingState();
        /// <inheritdoc/>
        protected override IBackgroundJobState CreateSystemDeletedState() => new SystemDeletedState();
        /// <inheritdoc/>
        protected override Task<bool> TryDeleteJobAsync(IStorageConnection connection, CancellationToken token = default)
        => connection.Storage.TryDeleteBackgroundJobAsync(Id, Lock?.LockedBy, connection, token);

        /// <inheritdoc/>
        public async Task<bool> SetSystemDeletedAsync(IStorageConnection connection, string reason = null, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));

            var deletedState = CreateSystemDeletedState();
            deletedState.Reason = reason;
            if (!await ChangeStateAsync(connection, deletedState, token).ConfigureAwait(false))
            {
                Logger.Warning($"Could not elect deleted state for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>.", Id, Environment);

                IsDeleted = false;
            }
            else
            {
                IsDeleted = true;
                
                await RaiseFinalStateElectedEvent(connection, deletedState, token).ConfigureAwait(false);
            }

            return IsDeleted;
        }
        #endregion

        #region Cancellation
        /// <inheritdoc/>
        protected override async Task SetCancelledStateAsync(IStorageConnection connection, string reason, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));

            await ChangeStateAsync(connection, new IdleState() { Reason = reason }, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override async Task ScheduleCancellationAction(IStorageConnection connection, string reason, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));

            await Job.ScheduleAction<CancelBackgroundJobAction>(connection, reason, true, 1, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override bool CanCancelJob()
        {
            if (!State.Name.In(EnqueuedState.StateName, ExecutingState.StateName))
            {
                Logger.Log($"Background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> is not in a valid state to be cancelled. Current state is <{HiveLog.Job.StateParam}>", Id, Environment, State.Name);
                return false;
            }
            return true;
        }
        #endregion

        /// <inheritdoc/>
        public override string ToString()
        {
            return Id.HasValue() ? $"Background job {Id}" : "New background job";
        }
        
    }
}
