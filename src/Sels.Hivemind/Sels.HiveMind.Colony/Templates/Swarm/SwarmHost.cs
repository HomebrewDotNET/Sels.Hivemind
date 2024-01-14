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
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using Sels.ObjectValidationFramework.Extensions.Validation;
using Sels.ObjectValidationFramework.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Base class for creating a swarm that processes jobs placed in queues of a supplied queue type.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by this host</typeparam>
    /// <typeparam name="TDefaultOptions">The type of default options used by this host</typeparam>
    public abstract class SwarmHost<TOptions, TDefaultOptions> 
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
        private readonly ITaskManager _taskManager;

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

        /// <inheritdoc cref="SwarmHost{TOptions}"/>
        /// <param name="queueType"><inheritdoc cref="QueueType"/></param>
        /// <param name="defaultOptions"><inheritdoc cref="_defaultOptions"/></param>
        /// <param name="taskManager">Used to manage dromes</param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        protected SwarmHost(string queueType, IOptionsMonitor<TDefaultOptions> defaultOptions, ITaskManager taskManager, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider)
        {
            QueueType = queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            _defaultOptions = defaultOptions.ValidateArgument(nameof(defaultOptions));
            _jobQueueProvider = jobQueueProvider.ValidateArgument(nameof(jobQueueProvider));
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));
            _schedulerProvider = schedulerProvider.ValidateArgument(nameof(schedulerProvider));
        }

        /// <summary>
        /// Runs the swarm host until <paramref name="token"/> gets cancelled.
        /// </summary>
        /// <param name="context">The contenxt of the daemon running the swarm host</param>
        /// <param name="token">Token that can be cancelled to stop the swarm from running</param>
        /// <returns>Task that will completed when <paramref name="token"/> gets cancelled</returns>
        public virtual async Task RunAsync(IDaemonExecutionContext context, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));

            context.Log($"Swarm host <{SwarmName}> starting");

            // Queue
            context.Log(LogLevel.Debug, $"Swarm host <{SwarmName}> creating job queue");
            await using var queueScope = await _jobQueueProvider.GetQueueAsync(context.Daemon.Colony.Environment, token).ConfigureAwait(false);
            var jobQueue = queueScope.Component;

            // Host
            context.Log(LogLevel.Debug, $"Swarm host <{SwarmName}> creating drone host");
            await using var droneHost = CreateDroneHost(Options, _defaultOptions.Get(context.Daemon.Colony.Environment), _taskManager, jobQueue, _schedulerProvider, context);
            context.Log(LogLevel.Debug, $"Swarm host <{SwarmName}> starting drone host <{HiveLog.Swarm.Name}>", droneHost.Options.Name);
            await droneHost.StartAsync(token).ConfigureAwait(false);

            // Sleep until cancellation
            try
            {
                context.StateGetter = () => droneHost.State;

                context.Log($"Swarm host <{SwarmName}> started");
                await Helper.Async.WaitUntilCancellation(token).ConfigureAwait(false);
                context.Log($"Swarm host <{SwarmName}> stopping");
                await droneHost.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                context.StateGetter = null;
            }

            context.Log($"Swarm host <{SwarmName}> stopped");
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

        private SwarmDroneHost CreateDroneHost(TOptions options, SwarmHostDefaultOptions defaultOptions, ITaskManager taskManager, IJobQueue jobQueue, IJobSchedulerProvider schedulerProvider, IDaemonExecutionContext context)
        {
            options.ValidateArgument(nameof(options));
            defaultOptions.ValidateArgument(nameof(defaultOptions));
            taskManager.ValidateArgument(nameof(taskManager));
            jobQueue.ValidateArgument(nameof(jobQueue));
            schedulerProvider.ValidateArgument(nameof(schedulerProvider));
            context.ValidateArgument(nameof(context));

            var childHosts = new List<SwarmDroneHost>();

            if (options.SubSwarmOptions.HasValue())
            {
                childHosts.AddRange(options.SubSwarmOptions.Select(x => CreateDroneHost(x, defaultOptions, taskManager, jobQueue, schedulerProvider, context)));
            }

            return new SwarmDroneHost(ProcessAsync, SwarmPrefix, QueueType, options, defaultOptions, childHosts, taskManager, jobQueue, schedulerProvider, context);
        }

        private class SwarmDroneHost : IAsyncDisposable
        {
            // Fields
            private readonly IJobQueue _jobQueue;
            private readonly IJobSchedulerProvider _schedulerProvider;
            private readonly IDaemonExecutionContext _context;
            private readonly ITaskManager _taskManager;
            private readonly SwarmHostDefaultOptions _defaultOptions;
            private readonly SwarmState _state;
            private readonly string _queueType;
            private readonly Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> _executeDelegate;

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
            public SwarmDroneHost(Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> executeDelegate, string swarmPrefix, string queueType, TOptions options, SwarmHostDefaultOptions defaultOptions, IEnumerable<SwarmDroneHost> subSwarms, ITaskManager taskManager, IJobQueue jobQueue, IJobSchedulerProvider schedulerProvider, IDaemonExecutionContext context)
            {
                _executeDelegate = executeDelegate.ValidateArgument(nameof(executeDelegate));
                swarmPrefix.ValidateArgumentNotNullOrWhitespace(nameof(swarmPrefix));
                _queueType = queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
                _taskManager = taskManager.ValidateArgument(nameof(taskManager));
                _jobQueue = jobQueue.ValidateArgument(nameof(jobQueue));
                _schedulerProvider = schedulerProvider.ValidateArgument(nameof(schedulerProvider));
                _context = context.ValidateArgument(nameof(context));
                _defaultOptions = defaultOptions.ValidateArgument(nameof(defaultOptions));
                SubSwarms = subSwarms.HasValue() ? subSwarms.ToArray() : null;

                _state = new SwarmState()
                {
                    Name = $"{swarmPrefix}{options.Name}",
                    Options = options.ValidateArgument(nameof(options))
                };
                _state.ChildSwarms = subSwarms.HasValue() ? subSwarms.Execute(x => x.Parent = this).Select(x => x.State).ToArray() : null;
            }

            /// <summary>
            /// Start the swarm host.
            /// </summary>
            /// <param name="token">Token used to cancel the start</param>
            /// <returns>Task that will complete when the swarm and any child swarms were started</returns>
            public async Task StartAsync(CancellationToken token)
            {
                _context.Log($"Starting swarm <{HiveLog.Swarm.Name}>", _state.Name);
                await _taskManager.ScheduleActionAsync(this, State.Name, false, x => RunAsync(x), x => x.WithManagedOptions(ManagedTaskOptions.KeepAlive | ManagedTaskOptions.GracefulCancellation), token).ConfigureAwait(false);

                if (SubSwarms.HasValue())
                {
                    _context.Log(LogLevel.Debug, $"Starting sub swarms for swarm <{HiveLog.Swarm.Name}>", _state.Name);
                    foreach (var subSwarm in SubSwarms)
                    {
                        _context.Log(LogLevel.Debug, $"Starting sub swarm <{HiveLog.Swarm.Name}> for swarm <{HiveLog.Swarm.Name}>", subSwarm._state.Name, _state.Name);
                        await subSwarm.StartAsync(token).ConfigureAwait(false);
                        _context.Log(LogLevel.Debug, $"Started sub swarm <{HiveLog.Swarm.Name}> for swarm <{HiveLog.Swarm.Name}>", subSwarm._state.Name, _state.Name);
                    }
                }

                _context.Log($"Started swarm <{HiveLog.Swarm.Name}>", _state.Name);
            }

            private async Task RunAsync(CancellationToken token)
            {
                try
                {
                    // Scheduler
                    var schedulerType = Options.SchedulerType;
                    if (!schedulerType.HasValue())
                    {
                        _context.Log(LogLevel.Debug, $"No scheduler type provided for swarm <{HiveLog.Swarm.Name}>. Using default", _state.Name);

                        if (_jobQueue.Features.HasFlag(JobQueueFeatures.Subscription)) schedulerType = _defaultOptions.SubscriptionSchedulerType;
                        else schedulerType = _defaultOptions.PollingSchedulerType;
                    }
                    var schedulerName = Options.SchedulerName.HasValue() ? Options.SchedulerName : _state.Name;
                    _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.Name}> will use scheduler of type <{schedulerType}> with name <{schedulerName}>", _state.Name);

                    // Drone amount
                    var droneAmount = Options.Drones.HasValue ? Options.Drones.Value : Parent == null ? _defaultOptions.RootSwarmDroneAmount : _defaultOptions.SubSwarmDroneAmount;
                    _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.Name}> will host <{droneAmount}> drones", _state.Name);

                    if (droneAmount > 0)
                    {
                        var drones = new List<DroneHost>();
                        _context.Log(LogLevel.Debug, $"Creating scheduler <{schedulerName}> of type <{schedulerType}> optimized for <{droneAmount}> drones for swarm <{HiveLog.Swarm.Name}", _state.Name);
                        await using var schedulerScope = await _schedulerProvider.CreateSchedulerAsync(schedulerType, schedulerName, _queueType, GetQueueGroups(), droneAmount, _jobQueue, token).ConfigureAwait(false);
                        var scheduler = schedulerScope.Component;
                        try
                        {
                            foreach (var droneNumber in Enumerable.Range(0, droneAmount))
                            {
                                var droneName = ('A' + droneNumber).ConvertTo<char>();
                                _context.Log($"Creating and starting drone <{droneName}> in swarm <{HiveLog.Swarm.Name}>", _state.Name);
                                var drone = new DroneHost(_executeDelegate, droneName.ToString(), this, _defaultOptions, scheduler, _context, _taskManager);
                                drones.Add(drone);
                                await drone.StartAsync(token).ConfigureAwait(false);
                                _context.Log($"Started drone <{droneName}> in swarm <{HiveLog.Swarm.Name}>", _state.Name);
                            }

                            _state.Drones = drones.Select(x => x.State).ToList();

                            _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.Name}> sleeping until cancellation", _state.Name);
                            await Helper.Async.WaitUntilCancellation(token).ConfigureAwait(false);
                            _context.Log(LogLevel.Debug, $"Swarm <{HiveLog.Swarm.Name}> cancelling. Stopping drones", _state.Name);
                        }
                        finally
                        {
                            await StopAsync(drones).ConfigureAwait(false);
                            _state.Drones = null;
                        }
                    }
                    else
                    {
                        _context.Log(LogLevel.Warning, $"Swarm <{HiveLog.Swarm.Name}> does not have any drones to host. Swarm will stop", _state.Name);
                    }
                }
                catch (Exception ex)
                {
                    _context.Log($"Swarm <{HiveLog.Swarm.Name}> could not properly start or stop all drones", ex, _state.Name);
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

                _context.Log($"Stopping swarm <{HiveLog.Swarm.Name}>", _state.Name);

                if (SubSwarms.HasValue())
                {
                    _context.Log(LogLevel.Debug, $"Stopping sub swarms for swarm <{HiveLog.Swarm.Name}>", _state.Name);
                    foreach (var subSwarm in SubSwarms)
                    {
                        try
                        {
                            _context.Log(LogLevel.Debug, $"Stopping sub swarm <{HiveLog.Swarm.Name}> for swarm <{HiveLog.Swarm.Name}>", subSwarm._state.Name, _state.Name);
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

                _context.Log(LogLevel.Debug, $"Waiting on swarm <{HiveLog.Swarm.Name}> to stop running", _state.Name);

                await Task.WhenAll(tasks).ConfigureAwait(false);

                _context.Log($"Stopped swarm <{HiveLog.Swarm.Name}>", _state.Name);

                if (exceptions.HasValue()) throw new AggregateException(exceptions);
            }

            /// <inheritdoc/>
            public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

            /// <summary>
            /// Returns all the queue groups the current swarm can work on ordered by priority.
            /// </summary>
            /// <returns>All the queue groups the current swarm can work on ordered by priority/returns>
            public IEnumerable<IEnumerable<string>> GetQueueGroups()
            {
                foreach (var queueGroup in GetQueues())
                {
                    yield return queueGroup;
                }

                if (!Options.IsDedicated && Parent != null)
                {
                    foreach (var queueGroup in Parent.GetQueueGroups())
                    {
                        yield return queueGroup;
                    }
                }
            }

            private IEnumerable<IEnumerable<string>> GetQueues()
            {
                Dictionary<byte, List<string>> grouped = null;
                List<string> noPriority = null;
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
                    foreach (var group in grouped.OrderBy(x => x.Key))
                    {
                        yield return group.Value;
                    }
                }

                if (noPriority.HasValue())
                {
                    yield return noPriority;
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
                public IReadOnlyList<IDroneState<TOptions>> Drones { get; set; }
                /// <inheritdoc/>
                [JsonIgnore]
                public ISwarmState<TOptions> Parent { get; set; }
                /// <inheritdoc/>
                public IReadOnlyList<ISwarmState<TOptions>> ChildSwarms { get; set; }
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
            private readonly SwarmHostDefaultOptions _defaultOptions;
            private readonly DroneState _state;

            // Properties
            /// <summary>
            /// The name of the drone.
            /// </summary>
            public string Name => _state.Name;
            /// <summary>
            /// The state of drone.
            /// </summary>
            public IDroneState<TOptions> State => _state;

            /// <inheritdoc cref="DroneHost"/>
            /// <param name="executeDelegate">The delegate that will be called to process any jobs received by the drone</param>
            /// <param name="name">The name of the drone</param>
            /// <param name="host">The swarm host the drone is from</param>
            /// <param name="parent">The drone host managing the current instance</param>
            /// <param name="defaultOptions"><inheritdoc cref="SwarmHostDefaultOptions"/></param>
            /// <param name="scheduler">The scheduler to use to request work</param>
            /// <param name="taskManager">Task manager used to manage worker task</param>
            /// <param name="context">The context of the daemon running the swarm host</param>
            public DroneHost(Func<IDaemonExecutionContext, IDroneState<TOptions>, IServiceProvider, IDequeuedJob, CancellationToken, Task> executeDelegate, string name, SwarmDroneHost parent, SwarmHostDefaultOptions defaultOptions, IJobScheduler scheduler, IDaemonExecutionContext context, ITaskManager taskManager)
            {
                _executeDelegate = executeDelegate.ValidateArgument(nameof(executeDelegate));
                _parent = parent.ValidateArgument(nameof(parent));
                _scheduler = scheduler.ValidateArgument(nameof(scheduler));
                _context = context.ValidateArgument(nameof(context));
                _defaultOptions = defaultOptions.ValidateArgument(nameof(defaultOptions));
                _taskManager = taskManager.ValidateArgument(nameof(taskManager));

                _state = new DroneState()
                {
                    Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name)),
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
                _context.Log($"Starting drone <{HiveLog.Swarm.DroneName}> in Swarm <{HiveLog.Swarm.Name}>", State.FullName, _state.Swarm.Name);
                await _taskManager.ScheduleActionAsync(this, State.FullName, false, x => RunAsync(x), x => x.WithManagedOptions(ManagedTaskOptions.KeepRunning | ManagedTaskOptions.GracefulCancellation), token).ConfigureAwait(false);
                _context.Log($"Started drone <{HiveLog.Swarm.DroneName}> in Swarm <{HiveLog.Swarm.Name}>", State.FullName, _state.Swarm.Name);
            }

            private async Task RunAsync(CancellationToken token)
            {
                _context.Log($"Drone <{HiveLog.Swarm.DroneName}> is now running", State.FullName);

                while (!token.IsCancellationRequested)
                {
                    var forceStopTokenSource = new CancellationTokenSource();
                    using var tokenScope = token.Register(() =>
                    {
                        var stoptime = _parent.Options.GracefulStoptime ?? _defaultOptions.GracefulStoptime;
                        _context.Log($"Drone <{HiveLog.Swarm.DroneName}> received cancellation request. No new jobs will be picked up. Current job will forcefully be cancelled in <{stoptime}>", State.FullName);
                        forceStopTokenSource.CancelAfter(stoptime);
                    });

                    try
                    {
                        _context.Log(LogLevel.Debug, $"Drone <{HiveLog.Swarm.DroneName}> requesting next job to process from scheduler", State.FullName);


                        var dequeuedJob = await _scheduler.RequestAsync(token).ConfigureAwait(false);
                        _state.SetProcessing(dequeuedJob);

                        using (new InProcessAction(x => _state.IsProcessing = x))
                        {
                            using var durationScope = Helper.Time.CaptureDuration(x => _context.Log($"Drone <{HiveLog.Swarm.DroneName}> handled job <{HiveLog.Job.Id}> in <{x.PrintTotalMs()}>", State.FullName, dequeuedJob.JobId));
                            await using (dequeuedJob)
                            {
                                _context.Log($"Drone <{HiveLog.Swarm.DroneName}> received job <{HiveLog.Job.Id}> from queue <{HiveLog.Job.Queue}> with a priority of <{HiveLog.Job.Priority}>", State.FullName, dequeuedJob.JobId, dequeuedJob.Queue, dequeuedJob.Priority);

                                // No lock management needed
                                if (dequeuedJob.IsSelfManaged)
                                {
                                    dequeuedJob.OnLockExpired(() =>
                                    {
                                        _context.Log(LogLevel.Warning, $"Lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> expired. Cancelling", dequeuedJob.JobId, State.FullName);
                                        forceStopTokenSource.Cancel();
                                        return Task.CompletedTask;
                                    });

                                    await ExecuteAsync(dequeuedJob, forceStopTokenSource.Token).ConfigureAwait(false);
                                }
                                // Manually manage lock
                                else
                                {
                                    var lockOffset = _state.Swarm.Options.LockExpirySafetyOffset ?? _defaultOptions.LockExpirySafetyOffset;
                                    // Try and set lock heartbeat if we are close to expiry
                                    if (DateTime.Now > dequeuedJob.ExpectedTimeout.Add(-lockOffset))
                                    {
                                        _context.Log(LogLevel.Debug, $"Lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> is about to expire or already expired. Trying to heartbeat to see if we still have lock", dequeuedJob.JobId, State.FullName);
                                        if (!await dequeuedJob.TryKeepAliveAsync(token).ConfigureAwait(false))
                                        {
                                            _context.Log(LogLevel.Warning, $"Lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> expired. Skipping", dequeuedJob.JobId, State.FullName);
                                            continue;
                                        }
                                    }

                                    // Start keep alive task
                                    var keepAliveTask = StartKeepAliveTask(dequeuedJob, lockOffset, forceStopTokenSource);

                                    try
                                    {
                                        await ExecuteAsync(dequeuedJob, forceStopTokenSource.Token).ConfigureAwait(false);
                                    }
                                    finally
                                    {
                                        await keepAliveTask.CancelAndWaitOnFinalization(token).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        var sleeptime = _parent.Options.UnhandledExceptionSleepTime ?? _defaultOptions.UnhandledExceptionSleepTime;
                        _context.Log($"Drone <{HiveLog.Swarm.DroneName}> ran into an issue. Fetching next job in <{sleeptime}>", ex, State.FullName);
                        await Helper.Async.Sleep(sleeptime, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _state.SetIdle();
                        forceStopTokenSource.Cancel();
                    }
                }

                _context.Log($"Drone <{HiveLog.Swarm.DroneName}> is now stopped", State.FullName);
            }

            private IDelayedPendingTask<IManagedTask> StartKeepAliveTask(IDequeuedJob dequeuedJob, TimeSpan lockOffset, CancellationTokenSource tokenSource)
            {
                dequeuedJob.ValidateArgument(nameof(dequeuedJob));
                tokenSource.ValidateArgument(nameof(tokenSource));

                return _taskManager.ScheduleDelayed(dequeuedJob.ExpectedTimeout.Add(-lockOffset), (m, t) =>
                {
                    return m.ScheduleActionAsync(this, "KeepAliveTask", false, async t =>
                    {
                        _context.Log(LogLevel.Debug, $"Keep alive task for dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> started", dequeuedJob.JobId, State.Name);

                        while (!t.IsCancellationRequested)
                        {
                            var setTime = dequeuedJob.ExpectedTimeout.Add(-lockOffset);
                            _context.Log(LogLevel.Debug, $"Keeping lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> alive at <{setTime}>", dequeuedJob.JobId, State.Name);
                            await Helper.Async.SleepUntil(setTime, t).ConfigureAwait(false);
                            if (t.IsCancellationRequested) return;

                            try
                            {
                                _context.Log(LogLevel.Debug, $"Lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> is about to expire or already expired. Trying to heartbeat to see if we still have lock", dequeuedJob.JobId, State.Name);
                                if (!await dequeuedJob.TryKeepAliveAsync(t).ConfigureAwait(false))
                                {
                                    _context.Log(LogLevel.Warning, $"Lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> expired. Cancelling", dequeuedJob.JobId, State.Name);
                                    tokenSource.Cancel();
                                    break;
                                }
                                else
                                {
                                    _context.Log(LogLevel.Debug, $"Kept lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> alive", dequeuedJob.JobId, State.Name);
                                }
                            }
                            catch (OperationCanceledException) when (t.IsCancellationRequested)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                _context.Log(LogLevel.Error, $"Could not keep lock on dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> alive. Cancelling", ex, dequeuedJob.JobId, State.Name);
                                tokenSource.Cancel();
                                break;
                            }
                        }
                        _context.Log(LogLevel.Debug, $"Keep alive task for dequeued job <{HiveLog.Job.Id}> for Drone <{HiveLog.Swarm.DroneName}> stopped", dequeuedJob.JobId, State.Name);
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
                _context.Log($"Stopping drone <{Name}> in Swarm <{_state.Swarm.Name}>");
                await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
                _context.Log($"Stopped drone <{Name}> in Swarm <{_state.Swarm.Name}>");
            }

            /// <inheritdoc/>
            public override string ToString() => $"DroneHost <{_state.Name}> managed by swarm host <{_parent.State.Name}>";

            private class DroneState : IDroneState<TOptions>
            {
                /// <inheritdoc/>
                [JsonIgnore]
                public ISwarmState<TOptions> Swarm { get; set; }
                /// <inheritdoc/>
                public string Name { get; set; }
                /// <inheritdoc/>
                public bool IsProcessing { get; set; }
                /// <inheritdoc/>
                public bool IsWorkingOnDedicated { get; set; }

                /// <inheritdoc/>
                public string JobId { get; set; }
                /// <inheritdoc/>
                public string JobQueue { get; set; }
                /// <inheritdoc/>
                public QueuePriority JobPriority { get; set; } = QueuePriority.None;

                /// <summary>
                /// Sets the state to that the drone is processing <paramref name="job"/>.
                /// </summary>
                /// <param name="job">The job the drone is processing</param>
                public void SetProcessing(IDequeuedJob job)
                {
                    job.ValidateArgument(nameof(job));
                    JobId = job.JobId;
                    JobQueue = job.Queue;
                    JobPriority = job.Priority;
                }

                /// <summary>
                /// Sets the state that the drone is idle.
                /// </summary>
                public void SetIdle()
                {
                    JobId = null;
                    JobQueue = null;
                    JobPriority = QueuePriority.None;
                }
            }
        }
    }
}
