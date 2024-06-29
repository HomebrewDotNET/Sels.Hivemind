using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Client;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job.Actions;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Models.Storage.Schedule;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Templates.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Manages the state of a recurring job.
    /// </summary>
    public class RecurringJob : BaseJob<IRecurringJobClient, IRecurringJobService, ILockedRecurringJob, RecurringJobChangeLog, IRecurringJobChangeTracker, RecurringJobStorageData, IRecurringJobState, JobStateStorageData, IRecurringJobAction>, ILockedRecurringJob
    {
        // State
        private ScheduleInfo _scheduleInfo;
        private RecurringJobSettings _settings;

        // Properties
        /// <inheritdoc/>
        public ISchedule Schedule => _scheduleInfo;
        /// <inheritdoc/>
        public IRecurringJobSettings Settings => _settings;
        /// <inheritdoc/>
        public DateTime? ExpectedExecutionDateUtc { get; set; }
        /// <inheritdoc/>
        public long ExecutedAmount { get; private set; }
        /// <inheritdoc/>
        public DateTime? LastStartedDateUtc { get; private set; }
        /// <inheritdoc/>
        public DateTime? LastCompletedDateUtc { get; private set; }

        /// <inheritdoc/>
        protected override RecurringJobChangeLog ChangeLog { get; set; } = new RecurringJobChangeLog();
        /// <inheritdoc/>
        protected override ILockedRecurringJob Job => this;

        /// <inheritdoc/>
        public override RecurringJobStorageData StorageData
        {
            get
            {
                var jobStorage = new RecurringJobStorageData(this, _settings, _scheduleInfo.StorageData, _invocation.StorageData, _lockData, _properties.Properties.Select(x => x.StorageData), _middleware.Select(x => x.StorageData), _options, Cache.Value);

                var states = new List<JobStateInfo<RecurringJobStorageData, IRecurringJobState, JobStateStorageData>>(_states);
                states.Reverse();
                foreach (var state in states)
                {
                    var storageState = state.StorageData;
                    storageState.Sequence = state.State.Sequence;
                    jobStorage.AddState(state.StorageData, state.IsInitialized && ChangeLog.NewStates.Contains(state.State));
                }

                return jobStorage;
            }
        }

        /// <summary>
        /// Creates a new instance from a persisted job.
        /// </summary>
        /// <param name="options"><inheritdoc cref="BaseJob{TState, TAction}._options"/></param>
        /// <param name="resolverScope"><inheritdoc cref="BaseJob{TState, TAction}._resolverScope"/></param>
        /// <param name="environment">The environment <paramref name="storageData"/> was retrieved from</param>
        /// <param name="storageData">The persisted state of the job</param>
        /// <param name="hasLock">True if the job was fetched with a lock, otherwise false for only reading the job</param>
        /// <param name="isCreation">True if the current recurring job is new, otherwise false</param>
        public RecurringJob(AsyncServiceScope resolverScope, HiveMindOptions options, string environment, RecurringJobStorageData storageData, bool hasLock, bool isCreation) : base(resolverScope, options, environment, storageData, hasLock)
        {
            IsCreation = isCreation;

            Set(storageData);

            if (isCreation)
            {
                SetState(new CreatedState());
            }
        }

        /// <summary>
        /// Updates the configuration of the current job.
        /// </summary>
        /// <param name="schedule"><inheritdoc cref="Schedule"/></param>
        /// <param name="settings"><inheritdoc cref="Settings"/></param>
        /// <param name="invocation"><inheritdoc cref="BaseJob{TClient, TService, TLockedJob, TChangeLog, TChangeTracker, TJobStorageData, TState, TStateStorageData, TAction}.Invocation"/></param>
        /// <param name="middleware"><inheritdoc cref="BaseJob{TClient, TService, TLockedJob, TChangeLog, TChangeTracker, TJobStorageData, TState, TStateStorageData, TAction}.Middleware"/></param>
        public void Set(ScheduleStorageData schedule, RecurringJobSettings settings, InvocationStorageData invocation, IEnumerable<MiddlewareStorageData> middleware)
        {
            Set(schedule.ValidateArgument(nameof(schedule)));
            Set(settings.ValidateArgument(nameof(settings)));
            Set(invocation.ValidateArgument(nameof(invocation)));
            if(middleware.HasValue()) Set(middleware);
        }

        #region Refresh
        /// <inheritdoc/>
        protected override void Set(RecurringJobStorageData data)
        {
            data.ValidateArgument(nameof(data));

            base.Set(data);

            _scheduleInfo = new ScheduleInfo(data.Schedule, _options, Cache.Value);
            _settings = data.Settings;

            if(data.States.HasValue()) Set(data.States);
        }
        private void Set(ScheduleStorageData data)
        {
            data.ValidateArgument(nameof(data));

            _scheduleInfo = new ScheduleInfo(data, _options, Cache.Value);
        }
        private void Set(RecurringJobSettings settings)
        {
            settings.ValidateArgument(nameof(settings));

            _settings = settings.ValidateArgument(nameof(settings));
        }
        /// <inheritdoc/>
        private void Set(IEnumerable<JobStateStorageData> data)
        {
            data.ValidateArgumentNotNullOrEmpty(nameof(data));

            _states = new List<JobStateInfo<RecurringJobStorageData, IRecurringJobState, JobStateStorageData>>(data.Select(x => new JobStateInfo< RecurringJobStorageData, IRecurringJobState, JobStateStorageData>(x, Environment, _options, Cache.Value)));
            _states.Reverse();
        }
        #endregion

        #region State management
        /// <inheritdoc/>
        protected override async Task<RequestResponse<IRecurringJobState>> RaiseStateElectionRequest(IStorageConnection storageConnection, IRecurringJobState state, CancellationToken token = default)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            state.ValidateArgument(nameof(state));
            return await Notifier.Value.RequestAsync(this, new RecurringJobStateElectionRequest(this, state, storageConnection), token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override async Task RaiseStateAppliedEvent(IStorageConnection storageConnection, IRecurringJobState state, CancellationToken token = default)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            state.ValidateArgument(nameof(state));
            await Notifier.Value.RaiseEventAsync(this, new RecurringJobStateAppliedEvent(this, storageConnection), token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override async Task RaiseFinalStateElectedEvent(IStorageConnection storageConnection, IRecurringJobState state, CancellationToken token = default)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            state.ValidateArgument(nameof(state));

            if (storageConnection.HasTransaction)
            {
                storageConnection.OnCommitted(t => Notifier.Value.RaiseEventAsync(this, new RecurringJobFinalStateElectedEvent(this, storageConnection), t));
            }
            else
            {
                await Notifier.Value.RaiseEventAsync(this, new RecurringJobFinalStateElectedEvent(this, storageConnection), token).ConfigureAwait(false);
            }
        }
        #endregion

        #region Update
        /// <inheritdoc/>
        public async Task  UpdateAsync(IStorageConnection connection, bool retainLock, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            if (!connection.Environment.EqualsNoCase(Environment)) throw new InvalidOperationException($"Cannot save changes to {this} in environment {Environment} with storage connection to environment {connection.Environment}");
            await using var lockScope = await _actionLock.LockAsync(token);

            lock (_lock)
            {
                if (IsDeleted) throw new InvalidOperationException($"Cannot save changes to deleted recurring job");
            }


            Logger.Log($"Updating recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);
            // Validate lock
            if (Id.HasValue()) await ValidateLock(token).ConfigureAwait(false);

            lock (_lock)
            {
                ModifiedAtUtc = DateTime.UtcNow;
            }
            await Notifier.Value.RaiseEventAsync(this, new RecurringJobUpdatingEvent(this, connection), token).ConfigureAwait(false);

            lock (_lock)
            {
                if (!retainLock) _hasLock = false;
            }

            var storageFormat = StorageData;
            await JobService.Value.TryUpdateAsync(connection, storageFormat, !retainLock, token).ConfigureAwait(false);
            lock (_lock)
            {
                if (!retainLock) _lockData = null;
                IsCommiting = true;
            }

            if (connection.HasTransaction)
            {
                // Register delegate to raise event if the current transaction is being commited
                connection.OnCommitting(async x => await RaiseOnUpdatedAsync(connection, x).ConfigureAwait(false));
            }
            else
            {
                await RaiseOnUpdatedAsync(connection, token).ConfigureAwait(false);
            }

            Logger.Log($"Saved changes made to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);
        }

        private async Task RaiseOnUpdatedAsync(IStorageConnection connection, CancellationToken token = default)
        {
            using var methodLogger = Logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));

            lock (_lock)
            {
                if (!IsCommiting) return;
                if (IsDisposed.HasValue) throw new ObjectDisposedException(nameof(RecurringJob));
            }

            await Notifier.Value.RaiseEventAsync(this, new RecurringJobUpdatedEvent(this, connection), x => x.Enlist(new RecurringJobFinalStateElectedEvent(this, connection))
                                                                                  , token).ConfigureAwait(false);

            lock (_lock)
            {
                IsCommiting = false;
                // Reset change log
                ChangeLog = new RecurringJobChangeLog();
            }
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(bool retainLock, CancellationToken token = default)
        {
            Logger.Debug($"Opening new connection to storage in environment <{HiveLog.EnvironmentParam}> for recurring job <{HiveLog.Job.IdParam}> to save changes", Environment, Id);

            await using (var connection = await JobClient.Value.OpenConnectionAsync(Environment, true, token).ConfigureAwait(false))
            {
                await UpdateAsync(connection.StorageConnection, retainLock, token).ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
            }
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

            await Job.ScheduleAction<CancelRecurringJobAction>(connection, reason, true, 1, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override bool CanCancelJob()
        {
            if (!State.Name.In(EnqueuedState.StateName, ExecutingState.StateName, SchedulingState.StateName))
            {
                Logger.Log($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> is not in a valid state to be cancelled. Current state is <{HiveLog.Job.StateParam}>", Id, Environment, State.Name);
                return false;
            }
            return true;
        }
        #endregion

        #region Deletion
        /// <inheritdoc/>
        protected override IRecurringJobState CreateSystemDeletingState() => new SystemDeletingState();
        /// <inheritdoc/>
        protected override IRecurringJobState CreateSystemDeletedState() => new SystemDeletedState();
        /// <inheritdoc/>
        protected override Task<bool> TryDeleteJobAsync(IStorageConnection connection, CancellationToken token = default)
        => connection.Storage.TryDeleteRecurringJobAsync(Id, Lock?.LockedBy, connection, token);
        #endregion

        /// <inheritdoc/>
        public override Task<IAsyncDisposable> AcquireStateLock(IStorageConnection connection, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override async Task TryUnlockJobAsync(IStorageConnection connection)
        {
            await connection.Storage.UnlockRecurringJobAsync(connection, Id, Lock?.LockedBy).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Recurring job {Id}";
        }
    }
}
