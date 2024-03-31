﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Conversion;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Mediator;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Exceptions.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
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
    public class RecurringJobClient : BaseClient, IRecurringJobClient
    {
        // Fields
        private readonly INotifier _notifier;
        private readonly IRecurringJobService _service;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<HiveMindOptions> _options;
        private readonly IMemoryCache _cache;

        /// <inheritdoc cref="BackgroundJobClient"/>
        /// <param name="notifier">Used to raised events</param>
        /// <param name="service">Service used to manage job state</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="cache">Optional memory cache that cam be used to speed up conversions</param>
        /// <param name="loggerFactory"><inheritdoc cref="BaseClient._loggerFactory"/></param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public RecurringJobClient(INotifier notifier, IRecurringJobService service, IServiceProvider serviceProvider, IOptionsMonitor<HiveMindOptions> options, IStorageProvider storageProvider, IMemoryCache cache = null, ILoggerFactory loggerFactory = null, ILogger<RecurringJobClient> logger = null) : base(storageProvider, loggerFactory, logger)
        {
            _notifier = notifier.ValidateArgument(nameof(notifier));
            _service = service.ValidateArgument(nameof(service));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }

        #region CreateOrUpdate
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync<T>(IStorageConnection connection, string id, Expression<Func<T, object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);
            typeof(T).ValidateArgumentInstanceable(nameof(T));

            _logger.Log($"Creating recurring job <{HiveLog.Job.Id}> of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync<T>(IStorageConnection connection, string id, Expression<Action<T>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);
            typeof(T).ValidateArgumentInstanceable(nameof(T));

            _logger.Log($"Creating recurring job <{HiveLog.Job.Id}>  of type <{typeof(T).GetDisplayName()}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo<T>(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync(IStorageConnection connection, string id, Expression<Func<object>> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);

            _logger.Log($"Creating static recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }
        /// <inheritdoc/>
        public Task<bool> CreateOrUpdateAsync(IStorageConnection connection, string id, Expression<Action> methodSelector, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            methodSelector.ValidateArgument(nameof(methodSelector));
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);

            _logger.Log($"Creating static recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            var invocationInfo = new InvocationInfo(methodSelector, _options.Get(connection.Environment), _cache);

            return CreateAsync(connection, id, invocationInfo, jobBuilder, token);
        }

        private async Task<bool> CreateAsync(IStorageConnection connection, string id, InvocationInfo invocationInfo, Func<IRecurringJobBuilder, IRecurringJobBuilder> jobBuilder, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            invocationInfo.ValidateArgument(nameof(invocationInfo));
            HiveMindHelper.Validation.ValidateRecurringJobId(id);

            var options = _options.Get(connection.Environment);
            var builder = new JobBuilder(this, connection, ConversionHelper.CopyTo<RecurringJobSettings, RecurringJobSettings>(options.RecurringJobSettings, forceConvert: true), options, _cache);
            if (jobBuilder != null) jobBuilder(builder);
            await _notifier.RaiseEventAsync(this, new RecurringJobConfiguringEvent(builder), token).ConfigureAwait(false);

            _logger.Log($"Trying to create or update recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            var recurringJobConfigurationStorageData = new RecurringJobConfigurationStorageData()
            {
                Id = id,
                InvocationData = invocationInfo.StorageData,
                Settings = builder.Settings,
                Middleware = builder.Middleware.HasValue() ? builder.Middleware.Select(x => x.StorageData).ToList() : null,
                Priority = builder.Priority,
                Queue = builder.Queue,
                Schedule = builder.ScheduleInfo.StorageData,
                Requester = builder.Requester,
                CreatedAt = DateTime.UtcNow
            };
            var recurringJobStorageData = await _service.TryCreateAsync(connection, recurringJobConfigurationStorageData, token).ConfigureAwait(false);

            bool isCreation = !recurringJobStorageData.States.HasValue();
            bool wasLocked = isCreation || (recurringJobStorageData.Lock != null && builder.Requester.Equals(recurringJobStorageData.Lock.LockedBy, StringComparison.OrdinalIgnoreCase));

            var recurringJob = new RecurringJob(_serviceProvider.CreateAsyncScope(), options, connection.Environment, recurringJobStorageData, wasLocked, isCreation);

            try
            {
                // Try acquired lock
                if (!recurringJob.HasLock)
                {
                    _logger.Debug($"Trying to acquire lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> so it can be updated", recurringJob.Id, recurringJob.Environment);

                    if (await recurringJob.TryLockAsync(connection, recurringJobConfigurationStorageData.Requester, token).ConfigureAwait(false) is (false, _))
                    {
                        switch (builder.UpdateBehaviour)
                        {
                            case RecurringJobUpdateBehaviour.Wait:
                                _logger.Debug($"Could not acquire lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> so it can be updated because it is currently held by <{HiveLog.Job.LockHolder}>. Waiting for job to be unlocked", recurringJob.Id, recurringJob.Environment, recurringJob?.Lock?.LockedBy);
                                break;
                            case RecurringJobUpdateBehaviour.Cancel:
                                _logger.Debug($"Could not acquire lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> so it can be updated because it is currently held by <{HiveLog.Job.LockHolder}>. Trying to cancel job if it's running and waiting for job to be unlocked", recurringJob.Id, recurringJob.Environment, recurringJob?.Lock?.LockedBy);
                                await recurringJob.CancelAsync(connection, recurringJobConfigurationStorageData.Requester, "Cancelling so job can be updated", token).ConfigureAwait(false);
                                break;
                            default: throw new NotSupportedException($"Recurring job update behaviour <{builder.UpdateBehaviour}> is not known");
                        }

                        var timeout = builder.UpdateTimeout ?? options.DefaultRecurringJobUpdateTimeout;
                        var pollingInterval = builder.UpdatePollingInterval;

                        // Try determine polling interval
                        if (!pollingInterval.HasValue)
                        {
                            _logger.Debug($"Trying to determine optimal lock polling interval for recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", recurringJob.Id, recurringJob.Environment);

                            var succeededStates = recurringJob.CastTo<IReadOnlyRecurringJob>().States.OfType<SucceededState>().ToList();

                            if (succeededStates.HasValue())
                            {
                                var averageExecutionTime = TimeSpan.FromMilliseconds(succeededStates.Select(x => x.Duration.TotalMilliseconds).Average());

                                if (timeout.HasValue && timeout.Value < averageExecutionTime)
                                {
                                    pollingInterval = timeout.Value / 3;
                                    _logger.Debug($"Average execution time for recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is <{averageExecutionTime}> but it is higher than the configured update timeout of <{timeout}>. Defaulting polling interval to <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
                                }
                                else
                                {
                                    pollingInterval = averageExecutionTime;
                                    _logger.Debug($"Average execution time for recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is <{averageExecutionTime}>. Using that as polling interval", recurringJob.Id, recurringJob.Environment);
                                }

                            }
                            else if (timeout.HasValue)
                            {
                                pollingInterval = timeout.Value / 3;
                                _logger.Debug($"Recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> never executed successfully before. Using timeout of <{timeout}> to set polling interval to <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
                            }
                            else
                            {
                                pollingInterval = TimeSpan.FromSeconds(5);
                                _logger.Debug($"Recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> never executed successfully before. Defaulting polling interval to <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
                            }
                        }

                        // Wait for lock
                        using (var timeoutTokenSource = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan))
                        {
                            using var tokenRegistration = token.Register(timeoutTokenSource.Cancel);

                            while (!recurringJob.HasLock)
                            {
                                // Sleep
                                _logger.Debug($"Trying to lock recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> again in <{pollingInterval}>", recurringJob.Id, recurringJob.Environment);
                                await Helper.Async.Sleep(pollingInterval.Value, timeoutTokenSource.Token).ConfigureAwait(false);
                                if (timeoutTokenSource.Token.IsCancellationRequested) throw new RecurringJobUpdateTimedoutException(recurringJob, recurringJobConfigurationStorageData.Requester);

                                if (await recurringJob.TryLockAsync(connection, recurringJobConfigurationStorageData.Requester, token).ConfigureAwait(false) is (true, _))
                                {
                                    _logger.Debug($"Acquired lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", recurringJob.Id, recurringJob.Environment);
                                }
                                else
                                {
                                    _logger.Debug($"Could not lock recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{recurringJobConfigurationStorageData.Requester}>", recurringJob.Id, recurringJob.Environment);
                                }
                            }
                        }
                    }
                }

                // Set config
                if (!isCreation)
                {
                    _logger.Debug($"Updating configuration on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", recurringJob.Id, recurringJob.Environment);
                    recurringJob.Set(recurringJobConfigurationStorageData.Schedule, recurringJobConfigurationStorageData.Settings, recurringJobConfigurationStorageData.InvocationData, recurringJobConfigurationStorageData.Middleware);

                    if (!recurringJobConfigurationStorageData.Queue.Equals(recurringJob.Queue) || !recurringJobConfigurationStorageData.Priority.Equals(recurringJob.Priority))
                    {
                        recurringJob.ChangeQueue(recurringJobConfigurationStorageData.Queue, recurringJobConfigurationStorageData.Priority);
                        _logger.Debug($"Updated queue and priority on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to <{recurringJob.Queue}> and <{recurringJob.Priority}>", recurringJob.Id, recurringJob.Environment);
                    }

                    if (builder.Properties.HasValue())
                    {
                        foreach (var (property, value) in builder.Properties)
                        {
                            recurringJob.SetProperty(property, value);
                            _logger.Debug($"Set property <{property}> on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", recurringJob.Id, recurringJob.Environment);
                        }
                    }
                }

                // Set state
                if (builder.ElectionState != null)
                {
                    _logger.Debug($"Electing state <{HiveLog.Job.State}> on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", builder.ElectionState.Name, recurringJob.Id, recurringJob.Environment);

                    await recurringJob.ChangeStateAsync(connection, builder.ElectionState, token).ConfigureAwait(false);
                }
                else if (isCreation)
                {
                    var state = new SchedulingState();
                    _logger.Debug($"Electing default state <{state}> on new recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state, recurringJob.Id, recurringJob.Environment);
                    await recurringJob.ChangeStateAsync(connection, state, token).ConfigureAwait(false);
                }

                // Save changes
                await recurringJob.UpdateAsync(connection, false, token).ConfigureAwait(false);

                await _notifier.RaiseEventAsync(this, new RecurringJobPersistedEvent(recurringJob, isCreation), token).ConfigureAwait(false);

                _logger.Log($"{(isCreation ? "Created" : "Updated")} recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);

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

            public JobBuilder(IRecurringJobClient client, IStorageConnection connection, RecurringJobSettings settings, HiveMindOptions options, IMemoryCache cache) : base(connection, options, cache)
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
            protected override void CheckMiddleware(Type type, object context)
            {
                type.ValidateArgumentAssignableTo(nameof(type), typeof(IRecurringJobMiddleware));
            }

            
        }
        #endregion
    }
}
