using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Colony.Scheduler;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Extensions;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Extensions.Validation;
using Sels.ObjectValidationFramework.Validators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Base class for creating a swarm that processes <see cref="IDequeuedJob"/>(s) placed in queues of a supplied queue type.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by this host</typeparam>
    /// <typeparam name="TDefaultOptions">The type of default options used by this host</typeparam>
    public abstract class SwarmHost<TOptions, TDefaultOptions> : ScheduledDaemon
        where TOptions : ISwarmHostOptions<TOptions>
        where TDefaultOptions : SwarmHostDefaultOptions
    {
        // Fields
        /// <summary>
        /// The default options configured.
        /// </summary>
        protected readonly IOptionsMonitor<TDefaultOptions> _defaultOptions;
        private readonly IJobQueueProvider _jobQueueProvider;
        private readonly IJobSchedulerProvider _schedulerProvider;

        // State

        // Properties
        /// <summary>
        /// The options used by this swarm.
        /// </summary>
        protected abstract TOptions Options { get; }
        /// <summary>
        /// The prefix that will be prepended to the root swarm name.
        /// </summary>
        protected abstract string SwarmPrefix { get; }
        /// <summary>
        /// The name of the root swarm.
        /// </summary>
        protected string SwarmName => $"{SwarmPrefix}{Options?.Name}";
        /// <summary>
        /// The type of queue to retrieve jobs from.
        /// </summary>
        public string QueueType { get; }
        /// <summary>
        /// Object containing the state of the swarm. 
        /// Can be used for tracing.
        /// Only set when the swarm is running.
        /// </summary>
        protected ISwarmState<TOptions>? SwarmState { get; private set; }
        /// <summary>
        /// The object that will be exposed as the daemon state.
        /// </summary>
        protected virtual object? DaemonState => SwarmState;

        /// <inheritdoc cref="SwarmHost{TOptions, TDefaultOptions}"/>
        /// <param name="queueType"><inheritdoc cref="QueueType"/></param>
        /// <param name="defaultOptions"><inheritdoc cref="_defaultOptions"/></param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        /// <param name="scheduleBuilder"><inheritdoc cref="ScheduledDaemon.Schedule"/></param>
        /// <param name="scheduleBehaviour"><inheritdoc cref="ScheduledDaemon.Behaviour"/></param>
        /// <param name="taskManager"><inheritdoc cref="ScheduledDaemon._taskManager"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="ScheduledDaemon._calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="ScheduledDaemon._intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="ScheduledDaemon._hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="ScheduledDaemon._cache"/></param>
        /// <param name="logger"><inheritdoc cref="ScheduledDaemon._logger"/></param>
        protected SwarmHost(string queueType, IOptionsMonitor<TDefaultOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null) : base(scheduleBuilder, scheduleBehaviour, true, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            QueueType = queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            _defaultOptions = defaultOptions.ValidateArgument(nameof(defaultOptions));
            _jobQueueProvider = jobQueueProvider.ValidateArgument(nameof(jobQueueProvider));
            _schedulerProvider = schedulerProvider.ValidateArgument(nameof(schedulerProvider));
        }

        /// <inheritdoc/>
        public override async Task Execute(IDaemonExecutionContext context, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));

            using var logScope = _logger.TryBeginScope(x => x.AddFluently(HiveLog.Daemon.Name, SwarmName));

            context.Log($"Swarm host starting");

            // Queue
            context.Log(LogLevel.Debug, $"Swarm host creating job queue");
            await using var queueScope = await _jobQueueProvider.CreateAsync(context.Daemon.Colony.Environment, token).ConfigureAwait(false);
            var jobQueue = queueScope.Component;

            // Host
            context.Log(LogLevel.Debug, $"Swarm host creating drone host");
            await using var droneHost = CreateDroneHost(Options, _defaultOptions.Get(context.Daemon.Colony.Environment), _taskManager, jobQueue, context.ServiceProvider.GetRequiredService<IActivator>(), _schedulerProvider, context, _logger);
            context.Log(LogLevel.Debug, $"Swarm host starting drone host <{HiveLog.Swarm.NameParam}>", droneHost.Options.Name);
            await droneHost.StartAsync(token).ConfigureAwait(false);

            // Sleep until cancellation
            var oldStateGetter = context.StateGetter;
            try
            {
                SwarmState = droneHost.State;
                context.StateGetter = () => DaemonState;

                context.Log($"Swarm host started");
                await Helper.Async.WaitUntilCancellation(token).ConfigureAwait(false);
                context.Log($"Swarm host stopping");
                await droneHost.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                context.StateGetter = oldStateGetter;
                SwarmState = null;
            }

            context.Log($"Swarm host stopped");
        }

        /// <summary>
        /// Processes <paramref name="job"/> received by a drone.
        /// </summary>
        /// <param name="context">The context of the daemon hosting the swarm</param>
        /// <param name="state">The state of the drone that is calling the method</param>
        /// <param name="serviceProvider">Service provider scoped to the processing of <paramref name="job"/> that can be used to resolve dependencies needed to process <paramref name="job"/></param>
        /// <param name="job">The job that was retrieved</param>
        /// <param name="token">Token that will be cancelled when the drone is requested to stop processing</param>
        /// <returns>Task that should complete when <paramref name="job"/> was processed</returns>
        protected abstract Task ProcessAsync(IDaemonExecutionContext context, IDroneState<TOptions> state, IServiceProvider serviceProvider, IDequeuedJob job, CancellationToken token);

        private SwarmDroneHost CreateDroneHost(TOptions options, SwarmHostDefaultOptions defaultOptions, ITaskManager taskManager, IJobQueue jobQueue, IActivator activator, IJobSchedulerProvider schedulerProvider, IDaemonExecutionContext context, ILogger? logger)
        {
            options.ValidateArgument(nameof(options));
            defaultOptions.ValidateArgument(nameof(defaultOptions));
            taskManager.ValidateArgument(nameof(taskManager));
            activator = Guard.IsNotNull(activator);
            jobQueue.ValidateArgument(nameof(jobQueue));
            schedulerProvider.ValidateArgument(nameof(schedulerProvider));
            context.ValidateArgument(nameof(context));

            var childHosts = new List<SwarmDroneHost>();

            if (options.SubSwarmOptions.HasValue())
            {
                childHosts.AddRange(options.SubSwarmOptions.Select(x => CreateDroneHost(x, defaultOptions, taskManager, jobQueue, activator, schedulerProvider, context, logger)));
            }

            return new SwarmDroneHost(ProcessAsync, SwarmPrefix, QueueType, options, defaultOptions, childHosts, taskManager, jobQueue, activator, schedulerProvider, context, logger);
        }

        private class SwarmDroneHost : IAsyncDisposable
        {
            // Fields
            private readonly IJobQueue _jobQueue;
            private readonly IJobSchedulerProvider _schedulerProvider;
            private readonly IDaemonExecutionContext _context;
            private readonly ITaskManager _taskManager;
            private readonly IActivator _activator;
            private readonly SwarmHostDefaultOptions _defaultOptions;
            private readonly SwarmState _state;
            private readonly string _queueType;
            private readonly Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> _executeDelegate;
            private readonly ILogger? _logger;

            // State
            private SwarmDroneHost _parent;

            // Properties
            /// <summary>
            /// The parent swarm. Will be null for the root swarm.
            /// </summary>
            public SwarmDroneHost Parent
            {
                get
                {
                    return _parent;
                }
                internal set
                {
                    _parent = value;
                    _state.Parent = _parent.State;
                    _state.Name = $"{_parent.State.Name}.{Options.Name}";
                }
            }
            /// <summary>
            /// Any sub swarms defined.
            /// </summary>
            public IReadOnlyCollection<SwarmDroneHost> SubSwarms { get; }
            /// <summary>
            /// The configured options for this swarm.
            /// </summary>
            public TOptions Options => _state.Options;
            /// <summary>
            /// The current state of the swarm.
            /// </summary>
            public ISwarmState<TOptions> State => _state;

            /// <inheritdoc cref="SwarmDroneHost"/>
            /// <param name="executeDelegate">The delegate that will be called to process any jobs received by the drones in the swarm</param>
            /// <param name="queueType">The type of the queue to request work from</param>
            /// <param name="options"><inheritdoc cref="Options"/></param>
            /// <param name="defaultOptions"><inheritdoc cref="SwarmHostDefaultOptions"/></param>
            /// <param name="subSwarms"><inheritdoc cref="SubSwarms"/></param>
            /// <param name="taskManager">Task manager used to manage drones</param>
            /// <param name="jobQueue">The job queue that wil lbe used to dequeue jobs</param>
            /// <param name="schedulerProvider">Used to resolve the scheduler for the swarm</param>
            /// <param name="context">The context of the daemon running the swarm host</param>
            /// <param name="logger">Optional logger for tracing</param>
            public SwarmDroneHost(Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> executeDelegate, string swarmPrefix, string queueType, TOptions options, SwarmHostDefaultOptions defaultOptions, IEnumerable<SwarmDroneHost> subSwarms, ITaskManager taskManager, IJobQueue jobQueue, IActivator activator, IJobSchedulerProvider schedulerProvider, IDaemonExecutionContext context, ILogger? logger)
            {
                _executeDelegate = executeDelegate.ValidateArgument(nameof(executeDelegate));
                swarmPrefix.ValidateArgumentNotNullOrWhitespace(nameof(swarmPrefix));
                _queueType = queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
                _taskManager = taskManager.ValidateArgument(nameof(taskManager));
                _activator = Guard.IsNotNull(activator);
                _jobQueue = jobQueue.ValidateArgument(nameof(jobQueue));
                _schedulerProvider = schedulerProvider.ValidateArgument(nameof(schedulerProvider));
                _context = context.ValidateArgument(nameof(context));
                _defaultOptions = defaultOptions.ValidateArgument(nameof(defaultOptions));
                SubSwarms = subSwarms.HasValue() ? subSwarms.ToArray() : null!;
                _logger = logger;

                _state = new SwarmState()
                {
                    Name = $"{swarmPrefix}{options.Name}",
                    Options = options.ValidateArgument(nameof(options))
                };
                _state.ChildSwarms = subSwarms.HasValue() ? subSwarms.Execute(x => x.Parent = this).Select(x => x.State).ToArray() : null!;
            }

            /// <summary>
            /// Start the swarm host.
            /// </summary>
            /// <param name="token">Token used to cancel the start</param>
            /// <returns>Task that will complete when the swarm and any child swarms were started</returns>
            public async Task StartAsync(CancellationToken token)
            {
                _context.Log($"Starting swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
                await _taskManager.ScheduleActionAsync(this, State.Name, false, x => RunAsync(x), x => x.WithManagedOptions(ManagedTaskOptions.KeepAlive | ManagedTaskOptions.GracefulCancellation), token).ConfigureAwait(false);

                if (SubSwarms.HasValue())
                {
                    _context.Log(LogLevel.Debug, $"Starting sub swarms for swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
                    foreach (var subSwarm in SubSwarms)
                    {
                        _context.Log(LogLevel.Debug, $"Starting sub swarm <{HiveLog.Swarm.NameParam}> for swarm <{HiveLog.Swarm.NameParam}>", subSwarm._state.Name, _state.Name);
                        await subSwarm.StartAsync(token).ConfigureAwait(false);
                        _context.Log(LogLevel.Debug, $"Started sub swarm <{HiveLog.Swarm.NameParam}> for swarm <{HiveLog.Swarm.NameParam}>", subSwarm._state.Name, _state.Name);
                    }
                }

                _context.Log($"Started swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
            }

            private async Task RunAsync(CancellationToken token)
            {
                try
                {
                    // Drone amount
                    var droneAmount = Options.Drones.HasValue ? Options.Drones.Value : Parent == null ? _defaultOptions.RootSwarmDroneAmount : _defaultOptions.SubSwarmDroneAmount;
                    if (droneAmount <= 0)
                    {
                        _context.Log(LogLevel.Warning, $"No drones configured for swarm <{HiveLog.Swarm.NameParam}>. Sleeping", _state.Name);
                        return;
                    }

                    // Scheduler middleware
                    _context.Log(LogLevel.Debug, $"Resolving scheduler middleware for swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
                    await using var activatorScope = await _activator.CreateActivatorScopeAsync(_context.ServiceProvider, token).ConfigureAwait(false);
                    var hiveOptions = _context.ServiceProvider.GetRequiredService<IOptionsSnapshot<HiveMindOptions>>().Get(_context.Daemon.Colony.Environment);
                    var cache = _context.ServiceProvider.GetService<IMemoryCache>();

                    HashSet<IComponent<IJobSchedulerMiddleware>>? createdMiddleware = null;
                    List<IJobSchedulerQueueGroup> queueGroups = new List<IJobSchedulerQueueGroup>();

                    // Resolve scheduler groups
                    try
                    {
                        foreach (var (queues, middlewareConfigs) in GetSchedulerGroups())
                        {
                            if (middlewareConfigs.HasValue())
                            {
                                var schedulerMiddleware = new List<(IJobSchedulerMiddleware Middleware, object? Context)>();
                                foreach (var middlewareConfig in middlewareConfigs!)
                                {
                                    if(middlewareConfig.Data != null)
                                    {
                                        var info = new MiddlewareInfo(middlewareConfig.Data, hiveOptions, cache);
                                        _context.Log(LogLevel.Debug, $"Activating scheduler middleware <{info.Type}> for swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
                                        var middleware = await activatorScope.ActivateAsync(info.Type, token).ConfigureAwait(false);
                                        schedulerMiddleware.Add((Guard.Is(middleware, x => x != null && x.IsAssignableTo<IJobSchedulerMiddleware>()).CastTo<IJobSchedulerMiddleware>(), info.Context));
                                    }
                                    else if(middlewareConfig.Factory != null)
                                    {
                                        _context.Log(LogLevel.Debug, $"Creating scheduler middleware for swarm <{HiveLog.Swarm.NameParam}> using factory", _state.Name);
                                        var component = await middlewareConfig.Factory(_context.ServiceProvider).ConfigureAwait(false);
                                        if (component != null)
                                        {
                                            createdMiddleware ??= new HashSet<IComponent<IJobSchedulerMiddleware>>();
                                            createdMiddleware.Add(component);
                                            schedulerMiddleware.Add((component.Component, middlewareConfig.Context));
                                        }
                                    }
                                    else throw new InvalidOperationException("No middleware data or factory provided");
                                }

                                queueGroups.Add(new JobSchedulerQueueGroup(queues, schedulerMiddleware));
                            }
                            else
                            {
                                queueGroups.Add(new JobSchedulerQueueGroup(queues, null));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Log($"Could not properly create scheduler middleware for swarm <{HiveLog.Swarm.NameParam}>", ex, _state.Name);
                        await createdMiddleware.ForceExecuteAsync(x => x.DisposeAsync().AsTask(), (x, e) => _context.Log($"Could not properly dispose middleware <{x}>", e)).ConfigureAwait(false);

                        throw;
                    }

                    await using var schedulerRequestPipeline = new SchedulerRequestPipeline(_jobQueue, createdMiddleware, _logger);

                    // Scheduler
                    _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.NameParam}> will host <{droneAmount}> drones", _state.Name);
                    var schedulerName = Options.SchedulerName.HasValue() ? Options.SchedulerName : _state.Name;
                    _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.NameParam}> will use scheduler with name <{schedulerName}>", _state.Name);

                    var schedulerConfiguration = new JobSchedulerConfiguration(schedulerName, _queueType, droneAmount);
                    var schedulerScope = await (Options.SchedulerFactory?.Invoke(_context.ServiceProvider, schedulerConfiguration) ?? Task.FromResult<IComponent<IJobScheduler>>(null!)).ConfigureAwait(false);

                    if (schedulerScope == null || schedulerScope.Component == null)
                    {
                        var schedulerType = Options.SchedulerType;
                        if (!schedulerType.HasValue())
                        {
                            _context.Log(LogLevel.Debug, $"No scheduler type provided for swarm <{HiveLog.Swarm.NameParam}>. Using default", _state.Name);

                            if (_jobQueue.Features.HasFlag(JobQueueFeatures.Subscription)) schedulerType = _defaultOptions.SubscriptionSchedulerType;
                            else schedulerType = _defaultOptions.PollingSchedulerType;
                        }
                        _context.Log(LogLevel.Debug, $"Creating scheduler <{schedulerName}> with name <{schedulerName}> of type <{schedulerType}> optimized for <{droneAmount}> drones for swarm <{HiveLog.Swarm.NameParam}", _state.Name);
                        schedulerScope = await _schedulerProvider.CreateAsync(schedulerType, schedulerConfiguration, token).ConfigureAwait(false);
                    }

                    if (droneAmount > 0)
                    {
                        var drones = new List<DroneHost>();

                        await using (schedulerScope.ValidateArgument(nameof(schedulerScope)))
                        {
                            var scheduler = schedulerScope.Component.ValidateArgument(nameof(schedulerScope.Component));
                            var schedulerQueues = new JobSchedulerQueues(queueGroups);
                            scheduler.Environment = _context.Daemon.Colony.Environment;
                            scheduler.Queues = schedulerQueues;
                            scheduler.RequestPipeline = schedulerRequestPipeline;
                            _context.Log($"Using <{scheduler}> as job scheduler in swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
                            try
                            {
                                _state.Scheduler = scheduler;
                                string[] ids;
                                var factory = Options.DroneIdGeneratorFactory;
                                await using (var factoryScope = Guard.IsNotNull(await factory(_context.ServiceProvider).ConfigureAwait(false)))
                                {
                                    var generator = Guard.IsNotNull(factoryScope.Component);

                                    _context.Log($"Using <{generator}> to generate <{droneAmount}> unique drone ids for swarm <{HiveLog.Swarm.NameParam}>", _state.Name);

                                    ids = await generator.GenerateRangeAsync(droneAmount).ConfigureAwait(false);
                                    _ = Guard.Is(ids, x => x != null && x.Length == droneAmount);
                                }

                                foreach (var id in ids)
                                {
                                    _context.Log($"Creating and starting drone <{HiveLog.Swarm.DroneAliasParam}> <{HiveLog.Swarm.DroneIdParam}> in swarm <{HiveLog.Swarm.NameParam}>", Options.DroneAlias, id, _state.Name);
                                    var drone = new DroneHost(_executeDelegate, Options.DroneAlias, id, this, _defaultOptions, scheduler, _context, _taskManager, _activator, _logger);
                                    drones.Add(drone);
                                    await drone.StartAsync(token).ConfigureAwait(false);
                                    _context.Log($"Started drone <{HiveLog.Swarm.DroneNameParam}> in swarm <{HiveLog.Swarm.NameParam}>", drone.Name, _state.Name);
                                }

                                _state.Drones = drones.Select(x => x.State).ToList();

                                _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.NameParam}> sleeping until cancellation", _state.Name);
                                await Helper.Async.WaitUntilCancellation(token).ConfigureAwait(false);
                                _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.NameParam}> cancelling. Stopping drones", _state.Name);
                            }
                            finally
                            {
                                await StopAsync(drones).ConfigureAwait(false);
                                _state.Drones = null;
                            }
                        }
                    }
                    else
                    {
                        _context.Log(LogLevel.Warning, $"Swarm <{HiveLog.Swarm.NameParam}> does not have any drones to host. Swarm will stop", _state.Name);
                    }
                }
                catch (Exception ex)
                {
                    _context.Log($"Swarm <{HiveLog.Swarm.NameParam}> could not properly start or stop all drones", ex, _state.Name);
                    throw;
                }
            }

            private async Task StopAsync(IEnumerable<DroneHost> drones)
            {
                drones.ValidateArgument(nameof(drones));
                var exceptions = new List<Exception>();
                var tasks = new List<Task>();

                foreach (var drone in drones)
                {
                    try
                    {
                        tasks.Add(drone.DisposeAsync().AsTask());
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                if (exceptions.HasValue()) throw new AggregateException(exceptions);
            }

            /// <summary>
            /// Stops the swarm and any sub swarms.
            /// </summary>
            /// <returns>Task that will complete when all swarms were stopped</returns>
            public async Task StopAsync()
            {
                var exceptions = new List<Exception>();
                var tasks = new List<Task>();

                _context.Log($"Stopping swarm <{HiveLog.Swarm.NameParam}>", _state.Name);

                if (SubSwarms.HasValue())
                {
                    _context.Log(LogLevel.Debug, $"Stopping sub swarms for swarm <{HiveLog.Swarm.NameParam}>", _state.Name);
                    foreach (var subSwarm in SubSwarms)
                    {
                        try
                        {
                            _context.Log(LogLevel.Debug, $"Stopping sub swarm <{HiveLog.Swarm.NameParam}> for swarm <{HiveLog.Swarm.NameParam}>", subSwarm._state.Name, _state.Name);
                            tasks.Add(subSwarm.StopAsync());
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }

                try
                {
                    tasks.Add(_taskManager.StopAllForAsync(this));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                _context.Log(LogLevel.Debug, $"Waiting on swarm <{HiveLog.Swarm.NameParam}> to stop running", _state.Name);

                await Task.WhenAll(tasks).ConfigureAwait(false);

                _context.Log($"Stopped swarm <{HiveLog.Swarm.NameParam}>", _state.Name);

                if (exceptions.HasValue()) throw new AggregateException(exceptions);
            }

            /// <inheritdoc/>
            public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

            private IEnumerable<(IEnumerable<string> Queues, IEnumerable<ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>>?)> GetSchedulerGroups()
            {
                // Determine current depth level of swarm
                var depth = 0;
                var parent = Parent;
                while (parent != null)
                {
                    depth++;
                    parent = parent.Parent;
                }

                // Loop over swarm options until we reach the root or the first dedicated swarm
                var currentDepth = depth;
                Dictionary<int, ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>[]> middlewarePerDepth = new Dictionary<int, ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>[]>();
                HashSet<string> returnedQueues = new HashSet<string>();
                var current = this;
                bool isDedicated = current.Options.IsDedicated;
                do
                {
                    var definedMiddleware = current.Options.SchedulerMiddleware;
                    List<ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>>? middleware = null;
                    if (definedMiddleware.HasValue())
                    {
                        middlewarePerDepth.Add(currentDepth, definedMiddleware.ToArray());
                        middleware = new List<ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>>(definedMiddleware);

                        // Check which middleware we can inherit from child swarms
                        foreach (var (middlewareDepth, childMiddleware) in middlewarePerDepth.Where(x => x.Key > currentDepth))
                        {
                            foreach (var potentialMiddleware in childMiddleware)
                            {
                                switch (potentialMiddleware.ConfigurationOptions.InheritanceBehaviour)
                                {
                                    case SwarmMiddlewareInheritanceBehaviour.Exclusive:
                                        continue;
                                    case SwarmMiddlewareInheritanceBehaviour.Inherit:
                                        // Inheritance is enabled. Check if there are depth limits configured
                                        var depthLimit = potentialMiddleware.ConfigurationOptions.MaxInheritanceDepth;
                                        var depthDifference = middlewareDepth - currentDepth;
                                        if (!depthLimit.HasValue || depthDifference <= depthLimit.Value)
                                        {
                                            middleware.Add(potentialMiddleware);
                                        }
                                        break;
                                    default: throw new NotSupportedException($"Inheritance behaviour <{potentialMiddleware.ConfigurationOptions.InheritanceBehaviour}> is not supported");
                                }
                            }
                        }
                    }

                    foreach (var queueGroup in current.GetQueueGroups(false))
                    {
                        var newQueueGroup = queueGroup.Where(x => !returnedQueues.Contains(x));
                        if (newQueueGroup.HasValue())
                        {
                            returnedQueues.Intersect(newQueueGroup);
                            yield return (newQueueGroup, middleware);
                        }
                    }
                    currentDepth--;
                    isDedicated = current.Options.IsDedicated;
                    current = current.Parent;
                }
                while (current != null && !isDedicated);
            }

            private IEnumerable<IEnumerable<string>> GetDistinctQueueGroups()
            {
                var returnedQueues = new HashSet<string>();

                foreach (var group in GetQueueGroups())
                {
                    var queues = group.Select(x => x.ToLower()).Where(x => !returnedQueues.Contains(x));

                    if (queues.HasValue())
                    {
                        returnedQueues.Intersect(queues);
                        yield return queues;
                    }
                }
            }
            /// <summary>
            /// Returns all the queue groups the current swarm can work on ordered by priority.
            /// </summary>
            /// <param name="includeParent">If queues from the parent should be included</param>
            /// <returns>All the queue groups the current swarm can work on ordered by priority/returns>
            public IEnumerable<IEnumerable<string>> GetQueueGroups(bool includeParent = true)
            {
                foreach (var queueGroup in GetQueues())
                {
                    yield return queueGroup;
                }

                if (!Options.IsDedicated && (includeParent && Parent != null))
                {
                    foreach (var queueGroup in Parent.GetQueueGroups())
                    {
                        yield return queueGroup;
                    }
                }
            }

            private IEnumerable<IEnumerable<string>> GetQueues()
            {
                Dictionary<byte, List<string>>? grouped = null;
                List<string>? noPriority = null;
                if (Options.Queues.HasValue())
                {
                    foreach (var queue in Options.Queues)
                    {
                        if (queue.Priority.HasValue)
                        {
                            grouped ??= new Dictionary<byte, List<string>>();
                            grouped.AddValueToList(queue.Priority.Value, queue.Name);
                        }
                        else
                        {
                            noPriority ??= new List<string>();
                            noPriority.Add(queue.Name);
                        }
                    }
                }

                if (Options.WorkOnGlobalQueue.HasValue ? Options.WorkOnGlobalQueue.Value : Parent == null)
                {
                    noPriority ??= new List<string>();
                    noPriority.Add(HiveMindConstants.Queue.DefaultQueue);
                }

                if (grouped.HasValue())
                {
                    foreach (var group in grouped!.OrderBy(x => x.Key))
                    {
                        yield return group.Value;
                    }
                }

                if (noPriority.HasValue())
                {
                    yield return noPriority!;
                }
            }

            /// <inheritdoc/>
            public override string ToString() => $"Swarm host <{_state.Name}>";

            private class SwarmState : ISwarmState<TOptions>
            {
                /// <inheritdoc/>
                public TOptions Options { get; set; }
                /// <inheritdoc/>
                public string Name { get; set; }
                /// <inheritdoc/>
                public IReadOnlyList<IDroneState<TOptions>>? Drones { get; set; }
                /// <inheritdoc/>
                [JsonIgnore]
                public ISwarmState<TOptions> Parent { get; set; }
                /// <inheritdoc/>
                public IReadOnlyList<ISwarmState<TOptions>> ChildSwarms { get; set; }
                /// <inheritdoc/>
                [JsonIgnore]
                public IJobScheduler Scheduler { get; set; }

                /// <inheritdoc/>
                public override string ToString()
                {
                    var builder = new StringBuilder();
                    ToString(builder);
                    return builder.ToString();
                }

                /// <summary>
                /// Appends the current state to <paramref name="builder"/>.
                /// </summary>
                /// <param name="builder">The builder to append to</param>
                /// <param name="currentIndent">The current currentIndent of the builder</param>
                public void ToString(StringBuilder builder, int currentIndent = 0)
                {
                    builder.ValidateArgument(nameof(builder));
                    currentIndent.ValidateArgumentLargerOrEqual(nameof(currentIndent), 0);

                    // Append swarm header
                    builder.Append('\t', currentIndent).Append('[').Append(Name).Append("]").Append(':').Append($"Processed={this.CastTo<ISwarmState<TOptions>>().Processed}");
                    if (ChildSwarms.HasValue())
                    {
                        // Count total processed by all childs
                        var childProcessed = ChildSwarms.Sum(x => x.Processed);
                        builder.Append("=>").Append(childProcessed).Append('(').Append(childProcessed + this.CastTo<ISwarmState<TOptions>>().Processed).Append(')').AppendLine();

                        // Append child swarms
                        currentIndent++;
                        for (int i = 0; i < ChildSwarms.Count; i++)
                        {
                            var childSwarm = ChildSwarms[i];
                            if(childSwarm is SwarmState swarmState)
                            {
                                swarmState.ToString(builder, currentIndent);
                                if (i < ChildSwarms.Count - 1)
                                {
                                    builder.AppendLine();
                                }
                            }
                        }
                        currentIndent--;
                    }
                    // Append drone state
                    if (Drones.HasValue())
                    {
                        builder.AppendLine();
                        foreach (var (droneState, i) in Drones!.Select((x, i) => (x, i)))
                        {
                            var isProcessing = droneState.IsProcessing;
                            builder.Append('\t', currentIndent).Append(' ', 2).Append(droneState.Name).Append('(').Append(isProcessing ? "ACTIVE" : "IDLE").Append(")");
                            if (isProcessing)
                            {
                                builder.Append(':').Append($"Job={droneState.JobId}|Queue={droneState.JobQueue}|Priority={droneState.JobPriority}|Duration={(droneState.Duration?.TotalMilliseconds ?? 0)}ms|DurationStats(Last/Min/Avg/Max)={droneState.LastDuration?.TotalMilliseconds ?? 0}/{droneState.MinDuration?.TotalMilliseconds ?? 0}/{droneState.AvgDuration?.TotalMilliseconds ?? 0}/{droneState.MaxDuration?.TotalMilliseconds ?? 0}ms|WaitStats(Last/Min/Avg/Max)={droneState.LastWait?.TotalMilliseconds ?? 0}/{droneState.MinWait?.TotalMilliseconds ?? 0}/{droneState.AvgWait?.TotalMilliseconds ?? 0}/{droneState.MaxWait?.TotalMilliseconds ?? 0}ms|Processed={droneState.Processed}");
                            }
                            else
                            {
                                builder.Append(':').Append($"DurationStats(Last/Min/Avg/Max)={droneState.LastDuration?.TotalMilliseconds ?? 0}/{droneState.MinDuration?.TotalMilliseconds ?? 0}/{droneState.AvgDuration?.TotalMilliseconds ?? 0}/{droneState.MaxDuration?.TotalMilliseconds ?? 0}ms|WaitStats(Last/Min/Avg/Max)={droneState.LastWait?.TotalMilliseconds ?? 0}/{droneState.MinWait?.TotalMilliseconds ?? 0}/{droneState.AvgWait?.TotalMilliseconds ?? 0}/{droneState.MaxWait?.TotalMilliseconds ?? 0}ms|Processed={droneState.Processed}");
                            }

                            if (i < Drones!.Count - 1)
                            {
                                builder.AppendLine();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Worker that asks for jobs from a scheduler
        /// </summary>
        private class DroneHost
        {
            // Fields
            private readonly Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> _executeDelegate;
            private readonly SwarmDroneHost _parent;
            private readonly IJobScheduler _scheduler;
            private readonly IDaemonExecutionContext _context;
            private readonly ITaskManager _taskManager;
            private readonly IActivator _activator;
            private readonly SwarmHostDefaultOptions _defaultOptions;
            private readonly DroneState _state;
            private readonly ILogger? _logger;

            // Properties
            /// <summary>
            /// The name of the drone.
            /// </summary>
            public string Name => State.Name;
            /// <summary>
            /// The state of drone.
            /// </summary>
            public IDroneState<TOptions> State => _state;

            /// <inheritdoc cref="DroneHost"/>
            /// <param name="executeDelegate">The delegate that will be called to process any jobs received by the drone</param>
            /// <param name="alias">The alias for the drone</param>
            /// <param name="id">The unique id of the drone</param>
            /// <param name="parent">The drone host managing the current instance</param>
            /// <param name="defaultOptions"><inheritdoc cref="SwarmHostDefaultOptions"/></param>
            /// <param name="scheduler">The scheduler to use to request work</param>
            /// <param name="taskManager">Task manager used to manage worker task</param>
            /// <param name="context">The context of the daemon running the swarm host</param>
            public DroneHost(Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> executeDelegate, string alias, string id, SwarmDroneHost parent, SwarmHostDefaultOptions defaultOptions, IJobScheduler scheduler, IDaemonExecutionContext context, ITaskManager taskManager, IActivator activator, ILogger? logger)
            {
                _executeDelegate = executeDelegate.ValidateArgument(nameof(executeDelegate));
                _parent = parent.ValidateArgument(nameof(parent));
                _scheduler = scheduler.ValidateArgument(nameof(scheduler));
                _context = context.ValidateArgument(nameof(context));
                _defaultOptions = defaultOptions.ValidateArgument(nameof(defaultOptions));
                _taskManager = taskManager.ValidateArgument(nameof(taskManager));
                _activator = Guard.IsNotNull(activator);
                _logger = logger;

                _state = new DroneState()
                {
                    Alias = Guard.IsNotNullOrWhitespace(alias),
                    Id = Guard.IsNotNullOrWhitespace(id),
                    Swarm = _parent.State
                };
            }

            /// <summary>
            /// Start the drone.
            /// </summary>
            /// <param name="token">Token used to cancel the start</param>
            /// <returns>Task that will complete when the swarm and any child swarms were started</returns>
            public async Task StartAsync(CancellationToken token)
            {
                _context.Log($"Starting drone <{HiveLog.Swarm.DroneNameParam}> in Swarm <{HiveLog.Swarm.NameParam}>", State.FullName, _state.Swarm.Name);
                await _taskManager.ScheduleActionAsync(this, State.FullName, false, x => RunAsync(x), x => x.WithManagedOptions(ManagedTaskOptions.KeepRunning | ManagedTaskOptions.GracefulCancellation), token).ConfigureAwait(false);
                _context.Log($"Started drone <{HiveLog.Swarm.DroneNameParam}> in Swarm <{HiveLog.Swarm.NameParam}>", State.FullName, _state.Swarm.Name);
            }

            private async Task RunAsync(CancellationToken token)
            {
                _context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> is now running", State.FullName);

                using var droneLogScope = _logger.TryBeginScope(x => x.AddFluently(HiveLog.Swarm.Name, State.Swarm.Name)
                                                                      .AddFluently(HiveLog.Swarm.DroneAlias, State.Alias)
                                                                      .AddFluently(HiveLog.Swarm.DroneId, State.Id)
                                                                      .AddFluently(HiveLog.Swarm.DroneName, State.Name)
                                                                      .AddFluently(HiveLog.Swarm.DroneFullName, State.FullName));

                while (!token.IsCancellationRequested)
                {
                    var forceStopTokenSource = new CancellationTokenSource();
                    using var tokenScope = token.Register(() =>
                    {
                        var stoptime = _parent.Options.GracefulStoptime ?? _defaultOptions.GracefulStoptime;
                        _context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> received cancellation request. No new jobs will be picked up. Current job will forcefully be cancelled in <{stoptime}>", State.FullName);
                        forceStopTokenSource.CancelAfter(stoptime);
                    });

                    try
                    {
                        _context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneFullNameParam}> requesting next job to process from scheduler", State.FullName);

                        IDequeuedJob dequeuedJob;
                        var stopwatch = new Stopwatch();
                        TimeSpan waitTime = default;
                        using (Helper.Time.CaptureDuration(t => waitTime = t))
                        {
                            dequeuedJob = await _scheduler.RequestAsync(token).ConfigureAwait(false);
                            stopwatch.Start();
                        }
                        _context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneFullNameParam}> got next job in <{waitTime.PrintTotalMs()}>", State.FullName);
                        _state.SetProcessing(dequeuedJob, waitTime);

                        using (new InProcessAction(x => _state.IsProcessing = x))
                        {
                            await using (dequeuedJob)
                            {
                                using var jobLogScope = _logger.TryBeginScope(x => x.AddFluently(HiveLog.Job.Id, dequeuedJob.JobId)
                                                                                    .AddFluently(HiveLog.Job.Queue, dequeuedJob.Queue)
                                                                                    .AddFluently(HiveLog.Job.Priority, dequeuedJob.Priority));
                                using var durationScope = Helper.Time.CaptureDuration(x => _context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneFullNameParam}> executed job <{HiveLog.Job.IdParam}> in <{x.PrintTotalMs()}>", State.FullName, dequeuedJob.JobId));
                                _context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> received job <{HiveLog.Job.IdParam}> from queue <{HiveLog.Job.QueueParam}> with a priority of <{HiveLog.Job.PriorityParam}>", State.FullName, dequeuedJob.JobId, dequeuedJob.Queue, dequeuedJob.Priority);

                                // Execute under valid lock
                                await using var jobLock = dequeuedJob.KeepAliveDuringScope(this, "KeepAliveTask", _taskManager, _state.Swarm.Options.LockExpirySafetyOffset ?? _defaultOptions.LockExpirySafetyOffset, () =>
                                {
                                    _context.Log(LogLevel.Warning, $"Lock on dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> expired. Cancelling", dequeuedJob.JobId, State.FullName);
                                    forceStopTokenSource.Cancel();
                                    return Task.CompletedTask;
                                }, _logger, token);

                                try
                                {
                                    await ExecuteAsync(dequeuedJob, forceStopTokenSource.Token).ConfigureAwait(false);
                                }
                                finally
                                {
                                    _state.Processed++;
                                }
                            }
                            stopwatch.Stop();
                        }
                        _context.Log(LogLevel.Information, $"Drone <{HiveLog.Swarm.DroneFullNameParam}> handled job <{HiveLog.Job.IdParam}> in <{stopwatch.Elapsed.PrintTotalMs()}>", State.FullName, dequeuedJob.JobId);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        var sleeptime = _parent.Options.UnhandledExceptionSleepTime ?? _defaultOptions.UnhandledExceptionSleepTime;
                        _context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> ran into an issue. Fetching next job in <{sleeptime}>", ex, State.FullName);
                        await Helper.Async.Sleep(sleeptime, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _state.SetIdle();
                        forceStopTokenSource.Cancel();
                    }
                }

                _context.Log($"Drone <{HiveLog.Swarm.DroneFullNameParam}> is now stopped", State.FullName);
            }

            private IDelayedPendingTask<IManagedTask> StartKeepAliveTask(IDequeuedJob dequeuedJob, TimeSpan lockOffset, CancellationTokenSource tokenSource)
            {
                dequeuedJob.ValidateArgument(nameof(dequeuedJob));
                tokenSource.ValidateArgument(nameof(tokenSource));

                return _taskManager.ScheduleDelayed(dequeuedJob.ExpectedTimeout.Add(-lockOffset), (m, t) =>
                {
                    return m.ScheduleActionAsync(this, "KeepAliveTask", false, async t =>
                    {
                        _context.Log(LogLevel.Information, $"Keep alive task for dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> started", dequeuedJob.JobId, State.FullName);

                        while (!t.IsCancellationRequested)
                        {
                            var setTime = dequeuedJob.ExpectedTimeout.Add(-lockOffset);
                            _context.Log(LogLevel.Debug, $"Keeping lock on dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> alive at <{setTime}>", dequeuedJob.JobId, State.FullName);
                            await Helper.Async.SleepUntil(setTime, t).ConfigureAwait(false);
                            if (t.IsCancellationRequested) return;

                            try
                            {
                                _context.Log(LogLevel.Debug, $"Lock on dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> is about to expire or already expired. Trying to heartbeat to see if we still have lock", dequeuedJob.JobId, State.FullName);
                                if (!await dequeuedJob.TryKeepAliveAsync(t).ConfigureAwait(false))
                                {
                                    _context.Log(LogLevel.Warning, $"Lock on dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> expired. Cancelling", dequeuedJob.JobId, State.FullName);
                                    tokenSource.Cancel();
                                    break;
                                }
                                else
                                {
                                    _context.Log(LogLevel.Information, $"Kept lock on dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> alive", dequeuedJob.JobId, State.FullName);
                                }
                            }
                            catch (OperationCanceledException) when (t.IsCancellationRequested)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                _context.Log(LogLevel.Error, $"Could not keep lock on dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> alive. Cancelling", ex, dequeuedJob.JobId, State.FullName);
                                tokenSource.Cancel();
                                break;
                            }
                        }
                        _context.Log(LogLevel.Information, $"Keep alive task for dequeued job <{HiveLog.Job.IdParam}> for Drone <{HiveLog.Swarm.DroneFullNameParam}> stopped", dequeuedJob.JobId, State.FullName);
                    }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation)
                             .WithPolicy(NamedManagedTaskPolicy.CancelAndStart)
                             .WithCreationOptions(TaskCreationOptions.PreferFairness)
                    , tokenSource.Token);
                });
            }

            private async Task ExecuteAsync(IDequeuedJob dequeuedJob, CancellationToken token)
            {
                dequeuedJob.ValidateArgument(nameof(dequeuedJob));

                await using var serviceScope = _context.ServiceProvider.CreateAsyncScope();

                await _executeDelegate(_context, State, serviceScope.ServiceProvider, dequeuedJob, token).ConfigureAwait(false);
            }

            /// <inheritdoc/>
            public async ValueTask DisposeAsync()
            {
                _context.Log($"Stopping drone <{HiveLog.Swarm.DroneNameParam}> in Swarm <{HiveLog.Swarm.NameParam}>", State.Name, _state.Swarm.Name);
                await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
                _context.Log($"Stopped drone <{HiveLog.Swarm.DroneNameParam}> in Swarm <{HiveLog.Swarm.NameParam}>", State.Name, _state.Swarm.Name);
            }

            /// <inheritdoc/>
            public override string ToString() => $"DroneHost <{State.Name}> managed by swarm host <{_parent.State.Name}>";

            private class DroneState : IDroneState<TOptions>
            {
                // Fields
                private readonly Stopwatch _stopwatch = new Stopwatch();

                // State
                private TimeSpan? _lastDuration;

                // Properties
                /// <inheritdoc/>
                [JsonIgnore]
                public ISwarmState<TOptions> Swarm { get; set; }
                /// <inheritdoc/>
                public string Alias { get; set; }
                /// <inheritdoc/>
                public string Id { get; set; }
                /// <inheritdoc/>
                public bool IsProcessing { get; set; }
                /// <inheritdoc/>
                public bool IsWorkingOnDedicated { get; set; }

                /// <inheritdoc/>
                public string? JobId { get; set; }
                /// <inheritdoc/>
                public string? JobQueue { get; set; }
                /// <inheritdoc/>
                public QueuePriority JobPriority { get; set; } = QueuePriority.None;
                /// <inheritdoc/>
                public TimeSpan? Duration => IsProcessing ? _stopwatch.Elapsed : (TimeSpan?)null;
                /// <inheritdoc/>
                public TimeSpan? LastDuration => _lastDuration;
                /// <inheritdoc/>
                public TimeSpan? LastWait { get; private set; }
                /// <inheritdoc/>
                public long Processed { get; set; }
                /// <inheritdoc/>
                public TimeSpan? MinDuration { get; private set; }
                /// <inheritdoc/>
                public TimeSpan? MaxDuration { get; private set; }
                /// <inheritdoc/>
                public TimeSpan? AvgDuration { get; private set; }
                /// <inheritdoc/>
                public TimeSpan? MinWait { get; private set; }
                /// <inheritdoc/>
                public TimeSpan? MaxWait { get; private set; }
                /// <inheritdoc/>
                public TimeSpan? AvgWait { get; private set; }

                /// <summary>
                /// Sets the state to that the drone is processing <paramref name="job"/>.
                /// </summary>
                /// <param name="job">The job the drone is processing</param>
                /// <param name="lastWait"><inheritdoc cref="LastWait"/></param>
                public void SetProcessing(IDequeuedJob job, TimeSpan lastWait)
                {
                    job.ValidateArgument(nameof(job));
                    LastWait = lastWait;
                    JobId = job.JobId;
                    JobQueue = job.Queue;
                    JobPriority = job.Priority;

                    if(!MinWait.HasValue || lastWait < MinWait.Value) MinWait = lastWait;
                    if(!MaxWait.HasValue || lastWait > MaxWait.Value) MaxWait = lastWait;
                    if(AvgWait.HasValue)
                    {
                        AvgWait = AvgWait + ((lastWait - AvgWait) / (Processed + 1));
                    }
                    else
                    {
                        AvgWait = lastWait;
                    }
                    _stopwatch.Restart();
                }

                /// <summary>
                /// Sets the state that the drone is idle.
                /// </summary>
                public void SetIdle()
                {
                    JobId = null;
                    JobQueue = null;
                    JobPriority = QueuePriority.None;
                    _stopwatch.Stop();
                    _lastDuration = _stopwatch.Elapsed;

                    if(!MinDuration.HasValue || _lastDuration < MinDuration.Value) MinDuration = _lastDuration;
                    if(!MaxDuration.HasValue || _lastDuration > MaxDuration.Value) MaxDuration = _lastDuration;
                    if(AvgDuration.HasValue)
                    {
                        AvgDuration = AvgDuration + ((_lastDuration - AvgDuration) / (Processed + 1));
                    }
                    else
                    {
                        AvgDuration = _lastDuration;
                    }
                }
            }
        }
    }
}
