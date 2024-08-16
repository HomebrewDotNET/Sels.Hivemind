using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Conversion;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Mediator;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Events.Job.Recurring;
using Sels.HiveMind.Exceptions.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.State.Recurring;
using Sels.HiveMind.Storage.Job.Recurring;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Templates.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Client
{
    /// <inheritdoc cref="IRecurringJobClient"/>
    public class RecurringJobClient : BaseJobClient<IRecurringJobService, IReadOnlyRecurringJob, ILockedRecurringJob, QueryRecurringJobOrderByTarget?, RecurringJobStorageData, IRecurringJobState, JobStateStorageData>, IRecurringJobClient
    {
        // Fields
        private readonly INotifier _notifier;
        private readonly IMemoryCache? _cache;

        /// <inheritdoc cref="BackgroundJobClient"/>
        /// <param name="notifier">Used to raised events</param>
        /// <param name="service">Service used to manage job state</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="cache">Optional memory cache that cam be used to speed up conversions</param>
        /// <param name="loggerFactory"><inheritdoc cref="BaseClient._loggerFactory"/></param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public RecurringJobClient(INotifier notifier, IRecurringJobService service, IServiceProvider serviceProvider, IOptionsMonitor<HiveMindOptions> options, IStorageProvider storageProvider, IMemoryCache? cache = null, ILoggerFactory? loggerFactory = null, ILogger<RecurringJobClient>? logger = null) : base(serviceProvider, options, service, storageProvider, loggerFactory, logger)
        {
            _notifier = notifier.ValidateArgument(nameof(notifier));
            _cache = cache;
        }

        #region CreateOrUpdate
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync<T>(IStorageConnection connection, string id, Expression<Func<T, object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder>? jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);
            typeof(T).ValidateArgumentInstanceable(nameof(T));

            _logger.Log($"Creating recurring job <{HiveLog.Job.IdParam}> of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync<T>(IStorageConnection connection, string id, Expression<Action<T>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder>? jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);
            typeof(T).ValidateArgumentInstanceable(nameof(T));

            _logger.Log($"Creating recurring job <{HiveLog.Job.IdParam}>  of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync(IStorageConnection connection, string id, Expression<Func<object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder>? jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);

            _logger.Log($"Creating static recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync(IStorageConnection connection, string id, Expression<Action> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder>? jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);

            _logger.Log($"Creating static recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }

        private async Task<bool> CreateAsync(IStorageConnection connection, string id, InvocationInfo invocationInfo, Func<IRecurringJobBuilder, IRecurringJobBuilder>? jobBuilder, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            invocationInfo.ValidateArgument(nameof(invocationInfo));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);

            var options = _options.Get(connection.Environment);
            var builder = new JobBuilder(this, connection, ConversionHelper.CopyTo<RecurringJobSettings, RecurringJobSettings>(options.RecurringJobSettings), options, _cache);
            if (jobBuilder != null) jobBuilder(builder);
            await _notifier.RaiseEventAsync(this, new RecurringJobConfiguringEvent(builder), token).ConfigureAwait(false);

            _logger.Log($"Trying to create or update recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            var recurringJobConfigurationStorageData = new RecurringJobConfigurationStorageData()
            {
                Id = id,
                InvocationData = invocationInfo.StorageData,
                Settings = builder.Settings,
                Middleware = builder.Middleware.HasValue() ? builder.Middleware.Select(x => x.StorageData).ToList() : null!,
                Priority = builder.Priority,
                Queue = builder.Queue,
                Schedule = builder.ScheduleInfo.StorageData,
                Requester = builder.Requester,
                CreatedAt = DateTime.UtcNow
            };
            var recurringJobStorageData = await _jobService.TryCreateAsync(connection, recurringJobConfigurationStorageData, token).ConfigureAwait(false);

            bool isCreation = !recurringJobStorageData.States.HasValue();
            bool wasLocked = isCreation || (recurringJobStorageData.Lock != null && builder.Requester.Equals(recurringJobStorageData.Lock.LockedBy, StringComparison.OrdinalIgnoreCase));

            var recurringJob = new RecurringJob(_serviceProvider.CreateAsyncScope(), options, connection.Environment, recurringJobStorageData, wasLocked, isCreation);

            try
            {
                // Try acquired lock
                if (!recurringJob.HasLock)
                {
                    _logger.Debug($"Trying to acquire lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> so it can be updated", recurringJob.Id, recurringJob.Environment);

                    if (await recurringJob.TryLockAsync(connection, recurringJobConfigurationStorageData.Requester, token).ConfigureAwait(false) is (false, _))
                    {
                        switch (builder.UpdateBehaviour)
                        {
                            case RecurringJobUpdateBehaviour.Wait:
                                _logger.Debug($"Could not acquire lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> so it can be updated because it is currently held by <{HiveLog.Job.LockHolderParam}>. Waiting for job to be unlocked", recurringJob.Id, recurringJob.Environment, recurringJob?.Lock?.LockedBy);
                                break;
                            case RecurringJobUpdateBehaviour.Cancel:
                                _logger.Debug($"Could not acquire lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> so it can be updated because it is currently held by <{HiveLog.Job.LockHolderParam}>. Trying to cancel job if it's running and waiting for job to be unlocked", recurringJob.Id, recurringJob.Environment, recurringJob?.Lock?.LockedBy);
                                await recurringJob!.CancelAsync(connection, recurringJobConfigurationStorageData.Requester, "Cancelling so job can be updated", token).ConfigureAwait(false);
                                break;
                            default: throw new NotSupportedException($"Recurring job update behaviour <{builder.UpdateBehaviour}> is not known");
                        }

                        var timeout = builder.UpdateTimeout ?? options.DefaultRecurringJobUpdateTimeout;
                        var pollingInterval = builder.UpdatePollingInterval;

                        // Try determine polling interval
                        if (!pollingInterval.HasValue)
                        {
                            pollingInterval = GetPollingInterval(recurringJob, timeout);
                        }

                        // Wait for lock
                        await recurringJob.WaitForLockAsync(connection, recurringJobConfigurationStorageData.Requester, pollingInterval ?? TimeSpan.FromSeconds(5), timeout, _logger, token).ConfigureAwait(false);
                    }
                }

                // Set config
                if (!isCreation)
                {
                    _logger.Debug($"Updating configuration on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", recurringJob.Id, recurringJob.Environment);
                    recurringJob.Set(recurringJobConfigurationStorageData.Schedule, recurringJobConfigurationStorageData.Settings, recurringJobConfigurationStorageData.InvocationData, recurringJobConfigurationStorageData.Middleware);

                    if (!recurringJobConfigurationStorageData.Queue.Equals(recurringJob.Queue) || !recurringJobConfigurationStorageData.Priority.Equals(recurringJob.Priority))
                    {
                        recurringJob.ChangeQueue(recurringJobConfigurationStorageData.Queue, recurringJobConfigurationStorageData.Priority);
                        _logger.Debug($"Updated queue and priority on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> to <{recurringJob.Queue}> and <{recurringJob.Priority}>", recurringJob.Id, recurringJob.Environment);
                    }
                }

                // Set properties
                if (builder.ClearPropertiesDuringUpdate && recurringJob.Properties.HasValue())
                {
                    foreach(var property in recurringJob.Properties)
                    {
                        recurringJob.RemoveProperty(property.Key);
                    }
                }
                if (builder.Properties.HasValue())
                {
                    foreach (var (property, value) in builder.Properties)
                    {
                        recurringJob.SetProperty(property, value);
                        _logger.Debug($"Set property <{property}> on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", recurringJob.Id, recurringJob.Environment);
                    }
                }

                // Set state
                if (builder.ElectionState != null)
                {
                    _logger.Debug($"Electing state <{HiveLog.Job.StateParam}> on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", builder.ElectionState.Name, recurringJob.Id, recurringJob.Environment);

                    await recurringJob.ChangeStateAsync(connection, builder.ElectionState, token).ConfigureAwait(false);
                }
                else if (isCreation)
                {
                    var state = new SchedulingState();
                    _logger.Debug($"Electing default state <{state}> on new recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", state, recurringJob.Id, recurringJob.Environment);
                    await recurringJob.ChangeStateAsync(connection, state, token).ConfigureAwait(false);
                }

                // Save changes
                await recurringJob.UpdateAsync(connection, false, token).ConfigureAwait(false);

                await _notifier.RaiseEventAsync(this, new RecurringJobPersistedEvent(recurringJob, isCreation), token).ConfigureAwait(false);

                _logger.Log($"{(isCreation ? "Created" : "Updated")} recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

                return isCreation;
            }
            finally
            {
                // Dispose job after connection closes
                if (connection.HasTransaction) connection.OnCommitted(async t => await recurringJob.DisposeAsync().ConfigureAwait(false));
                else await recurringJob.DisposeAsync().ConfigureAwait(false);
            }
        }
        #endregion

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(IStorageConnection connection, string id, string? requester = null, TimeSpan? pollingInterval = null, TimeSpan? timeout = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));

            _logger.Log($"Preparing to delete recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            requester ??= $"UnknownClient_{Guid.NewGuid()}";
            var recurringJob = await GetAndTryLockAsync(connection, id, requester, token).ConfigureAwait(false);
            bool wasDeleted = false;
            try
            {
                if (!recurringJob.HasLock)
                {
                    _logger.Warning($"Could not acquire lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> so it can be deleted. Cancelling job and waiting for lock", id, connection.Environment);
                    await recurringJob.CancelAsync(connection, requester, "Cancelling so job can be deleted", token).ConfigureAwait(false);

                    // Try determine polling interval
                    if (!pollingInterval.HasValue)
                    {
                        pollingInterval = GetPollingInterval(recurringJob, timeout);
                    }
                }

                _logger.Log($"Waiting for lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                var lockedJob = await recurringJob.WaitForLockAsync(connection, requester, pollingInterval ?? TimeSpan.FromSeconds(5), timeout, _logger, token).ConfigureAwait(false);
                _logger.Log($"Got lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Triggering deletion");

                wasDeleted = await lockedJob.SystemDeleteAsync(connection, requester, token).ConfigureAwait(false);
                if (wasDeleted)
                {
                    _logger.Log($"Deleted recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                }
                else
                {
                    _logger.Warning($"Could not delete recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                }
            }
            finally
            {
                // Dispose job after connection closes
                if (connection.HasTransaction) connection.OnCommitted(async t => await recurringJob.DisposeAsync().ConfigureAwait(false));
                else await recurringJob.DisposeAsync().ConfigureAwait(false);
            }

            return wasDeleted;
        }

        private TimeSpan GetPollingInterval(IReadOnlyRecurringJob recurringJob, TimeSpan? timeout)
        {
            _logger.Debug($"Trying to determine optimal lock polling interval for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", recurringJob.Id, recurringJob.Environment);

            var succeededStates = recurringJob.CastTo<IReadOnlyRecurringJob>().States.OfType<SucceededState>().ToList();

            TimeSpan pollingInterval = TimeSpan.FromSeconds(5);
            if (succeededStates.HasValue())
            {
                var averageExecutionTime = TimeSpan.FromMilliseconds(succeededStates.Select(x => x.Duration.TotalMilliseconds).Average());

                if (timeout.HasValue && timeout.Value < averageExecutionTime)
                {
                    pollingInterval = timeout.Value / 3;
                    _logger.Debug($"Average execution time for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> is <{averageExecutionTime}> but it is higher than the configured update timeout of <{timeout}>. Defaulting polling interval to <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
                }
                else
                {
                    pollingInterval = averageExecutionTime;
                    _logger.Debug($"Average execution time for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> is <{averageExecutionTime}>. Using that as polling interval", recurringJob.Id, recurringJob.Environment);
                }

            }
            else if (timeout.HasValue)
            {
                pollingInterval = timeout.Value / 3;
                _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> never executed successfully before. Using timeout of <{timeout}> to set polling interval to <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
            }
            else
            {
                pollingInterval = TimeSpan.FromSeconds(5);
                _logger.Debug($"Recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> never executed successfully before. Defaulting polling interval to <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
            }

            return pollingInterval;
        }

        /// <inheritdoc/>
        protected override IReadOnlyRecurringJob CreateReadOnlyJob(AsyncServiceScope serviceScope, HiveMindOptions options, string environment, RecurringJobStorageData storageData, bool hasLock)
        => new RecurringJob(serviceScope, options, environment, storageData, hasLock, false);
        /// <inheritdoc/>
        protected override ILockedRecurringJob CreateLockedJob(AsyncServiceScope serviceScope, HiveMindOptions options, string environment, RecurringJobStorageData storageData)
         => new RecurringJob(serviceScope, options, environment, storageData, true, false);
        /// <inheritdoc/>
        protected override Task<RecurringJobStorageData> TryGetJobDataAsync(string id, IStorageConnection connection, CancellationToken token = default)
        => connection.Storage.GetRecurringJobAsync(id, connection, token);
        /// <inheritdoc/>
        protected override IClientQueryResult<ILockedRecurringJob> CreateReadOnlyQueryResult(string environment, IReadOnlyList<IReadOnlyRecurringJob> jobs)
        {
            return new QueryResult<RecurringJob>(this, environment, jobs.OfType<RecurringJob>(), false);
        }
        /// <inheritdoc/>
        protected override IClientQueryResult<ILockedRecurringJob> CreateLockedQueryResult(string environment, IReadOnlyList<ILockedRecurringJob> jobs, bool isTimedOut)
        {
            return new QueryResult<RecurringJob>(this, environment, jobs.OfType<RecurringJob>(), isTimedOut);
        }
        /// <inheritdoc/>
        protected override Task<string[]> GetDistinctQueues(IStorageConnection connection, string? prefix = null, CancellationToken token = default)
        => connection.Storage.GetAllBackgroundJobQueuesAsync(connection, prefix, token);


        #region Classes
        private class JobBuilder : BaseJobBuilder<IRecurringJobBuilder>, IRecurringJobBuilder
        {
            // Properties
            /// <inheritdoc/>
            public IRecurringJobClient Client { get; }
            /// <inheritdoc/>
            public IRecurringJobState ElectionState { get; private set; }
            /// <inheritdoc/>
            public RecurringJobSettings Settings { get; }
            /// <inheritdoc/>
            public RecurringJobUpdateBehaviour UpdateBehaviour { get; private set; }
            /// <inheritdoc/>
            public TimeSpan? UpdateTimeout { get; private set; }
            /// <inheritdoc/>
            public TimeSpan? UpdatePollingInterval { get; private set; }
            /// <inheritdoc/>
            public string Requester { get; private set; }
            /// <inheritdoc/>
            public ScheduleInfo ScheduleInfo { get; private set; }
            /// <inheritdoc/>
            protected override IRecurringJobBuilder Builder => this;
            /// <inheritdoc/>
            IRecurringJobSettings IRecurringJobBuilder.Settings => Settings;
            /// <inheritdoc/>
            ISchedule IRecurringJobBuilder.Schedule => ScheduleInfo;
            /// <inheritdoc/>
            public bool ClearPropertiesDuringUpdate { get; private set; }

            public JobBuilder(IRecurringJobClient client, IStorageConnection connection, RecurringJobSettings settings, HiveMindOptions options, IMemoryCache? cache) : base(connection, options, cache)
            {
                Client = client.ValidateArgument(nameof(client));
                Settings = settings.ValidateArgument(nameof(settings));

                UsingUpdateBehaviour(RecurringJobUpdateBehaviour.Wait);
                UsingUpdateTimeout(options.DefaultRecurringJobUpdateTimeout);
                RequestedBy($"UnknownClient_{Guid.NewGuid()}");
                WithSchedule(x => x.RunEvery(TimeSpan.FromMinutes(1)));
            }

            /// <inheritdoc/>
            public IRecurringJobBuilder WithSchedule(Action<IScheduleBuilder> scheduleBuilder)
            {
                scheduleBuilder.ValidateArgument(nameof(scheduleBuilder));
                var builder = new ScheduleBuilder(scheduleBuilder, _options, _cache);
                ScheduleInfo = builder.ScheduleInfo;
                return this;
            }

            /// <inheritdoc/>
            public IRecurringJobBuilder InState(IRecurringJobState state)
            {
                ElectionState = state.ValidateArgument(nameof(state));
                return this;
            }
            /// <inheritdoc/>
            public IRecurringJobBuilder Configure(Action<RecurringJobSettings> configurator)
            {
                configurator.ValidateArgument(nameof(configurator));
                configurator(Settings);
                return this;
            }
            /// <inheritdoc/>
            public IRecurringJobBuilder RequestedBy(string requester)
            {
                Requester = requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
                return this;
            }
            /// <inheritdoc/>
            public IRecurringJobBuilder UsingUpdateBehaviour(RecurringJobUpdateBehaviour behaviour)
            {
                UpdateBehaviour = behaviour;
                return this;
            }
            /// <inheritdoc/>
            public IRecurringJobBuilder UsingUpdateTimeout(TimeSpan? timeout)
            {
                UpdateTimeout = timeout;
                return this;
            }
            /// <inheritdoc/>
            public IRecurringJobBuilder UsingUpdatePollingInterval(TimeSpan? interval)
            {
                UpdatePollingInterval = interval;
                return this;
            }
            /// <inheritdoc/>
            public IRecurringJobBuilder ClearProperties()
            {
                ClearPropertiesDuringUpdate = true;
                return this;
            }
            /// <inheritdoc/>
            protected override void CheckMiddleware(Type type, object context)
            {
                type.ValidateArgumentAssignableTo(nameof(type), typeof(IRecurringJobMiddleware));
            }
        }

        private class QueryResult<T> : IClientQueryResult<T> where T : RecurringJob
        {
            // Fields
            private readonly IRecurringJobClient _client;
            private readonly string _environment;
            private readonly bool _isTimedOut;

            // Properties
            /// <inheritdoc/>
            public IReadOnlyList<T> Results { get; }

            public QueryResult(IRecurringJobClient client, string environment, IEnumerable<T> results, bool isTimedOut)
            {
                _client = client.ValidateArgument(nameof(client));
                _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
                Results = results.ValidateArgument(nameof(results)).ToArray();
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
                                await connection.StorageConnection.Storage.UnlockRecurringJobsAsync(ids.ToArray(), holder, connection.StorageConnection).ConfigureAwait(false);
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
