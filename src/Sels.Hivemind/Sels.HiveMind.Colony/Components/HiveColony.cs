using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Dispose;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.Core.Extensions.Validation;
using Sels.Core.Mediator;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Colony.Events;
using Sels.HiveMind.Extensions;
using Sels.HiveMind.Job.State.Background;
using Sels.HiveMind.Service;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IColony"/>
    public class HiveColony : IColony, IColonyBuilder, IColonyConfigurator, ILockable, IAsyncExposedDisposable
    {
        // Constants
        private const string ManagementTaskName = "ManageColony";
        private const string LockMaintainerTaskName = "MaintainLock";
        private const string StateSyncTaskName = "StateSync";
        private const string DaemonManagerTaskName = "ManageDaemons";

        // Fieds
        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _managementLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _statusLock = new SemaphoreSlim(1, 1);
        private readonly IColonyService _colonyService;
        private readonly INotifier _notifier;
        private readonly AsyncServiceScope _scope;
        private readonly ITaskManager _taskManager;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger? _logger;
        private readonly string _requester;
        private readonly IOptionsMonitor<HiveMindOptions> _hiveOptions;

        // State
        private readonly Dictionary<string, Daemon> _daemons = new Dictionary<string, Daemon>(StringComparer.OrdinalIgnoreCase);
        private ColonyOptions _options;
        private bool _hasLock;

        // Properties
        /// <inheritdoc/>
        public IReadOnlyList<IDaemon> Daemons { get { lock (_stateLock) { return _daemons.Values.ToList(); } } }
        /// <inheritdoc/>
        IReadOnlyList<IReadOnlyDaemon> IReadOnlyColony.Daemons => Daemons;
        /// <inheritdoc/>
        IReadOnlyColony IColonyBuilder.Current => this;
        /// <inheritdoc/>
        private IColonyBuilder Builder => this;
        /// <inheritdoc/>
        private IReadOnlyColony Self => this;
        /// <inheritdoc/>
        public string Id { get; private set; }
        /// <inheritdoc/>
        public string Name { get; private set; }
        /// <inheritdoc/>
        public string Environment { get; private set; }
        /// <inheritdoc/>
        public ILockInfo Lock { get; private set; }
        /// <inheritdoc/>
        public ColonyStatus Status { get; private set; }
        /// <inheritdoc/>
        public IColonyOptions Options => _options;
        private HiveMindOptions HiveOptions => _hiveOptions.Get(Environment);
        /// <inheritdoc/>
        public object SyncRoot { get; } = new object();
        /// <inheritdoc/>
        public ConcurrentDictionary<string, object> LocalProperties { get; } = new ConcurrentDictionary<string, object>();
        /// <inheritdoc/>
        public ConcurrentDictionary<string, object> Properties { get; } = new ConcurrentDictionary<string, object>();
        /// <inheritdoc/>
        IReadOnlyList<IWriteableDaemon> IWriteableColony.Daemons => Daemons;
        /// <inheritdoc/>
        IReadOnlyDictionary<string, object> IReadOnlyColony.LocalProperties => LocalProperties;
        /// <inheritdoc/>
        IReadOnlyList<IDaemonInfo> IColonyInfo.Daemons => Daemons;
        /// <inheritdoc/>
        IReadOnlyDictionary<string, object> IColonyInfo.Properties => Properties;
        /// <inheritdoc/>
        public DateTime ExpectedTimeoutUtc => _hasLock && Lock != null ? Lock.LockHeartbeatUtc + HiveOptions.LockTimeout : DateTime.UtcNow;
        /// <inheritdoc/>
        bool ILockable.IsSelfManaged => false;
        /// <inheritdoc/>
        bool ILockable.CanNotifyExpiry => false;
        /// <inheritdoc/>
        public bool IsExpired => !_hasLock || Lock == null || ExpectedTimeoutUtc <= DateTime.UtcNow;
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }

        /// <inheritdoc cref="HiveColony"/>
        /// <param name="configurator">Delegate that configures this instance</param>
        /// <param name="serviceScope">The service scope that shares the same lifetime as the colony. Used to resolve dependencies</param>
        /// <param name="colonyService">Used to manage the colony</param>
        /// <param name="notifier">Used to raise events</param>
        /// <param name="taskManager">Used to manage background tasks</param>
        /// <param name="hiveOptions">Used to fetch the configured options for the environment</param>
        /// <param name="identityProvider">Used to generate a unique name for this instance if <paramref name="configurator"/> does not provide one</param>
        /// <param name="loggerFactory">Optional logger factory for creating loggers for the daemons</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveColony(Action<IColonyBuilder> configurator, AsyncServiceScope serviceScope, IColonyService colonyService, INotifier notifier, ITaskManager taskManager, IOptionsMonitor<HiveMindOptions> hiveOptions, IColonyIdentityProvider? identityProvider = null, ILoggerFactory? loggerFactory = null, ILogger<HiveColony>? logger = null)
        {
            configurator.ValidateArgument(nameof(configurator));
            identityProvider.ValidateArgument(nameof(identityProvider));
            _scope = serviceScope;
            _colonyService = colonyService;
            _notifier = notifier.ValidateArgument(nameof(notifier));
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));
            _loggerFactory = loggerFactory;
            _hiveOptions = Guard.IsNotNull(hiveOptions);
            _logger = logger;
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                _requester = $"{System.Environment.MachineName}({currentProcess.Id}):{Guid.NewGuid()}";
            }

            Builder.InEnvironment(HiveMindConstants.DefaultEnvironmentName);
            Builder.WithOptions(new ColonyOptions());
            configurator(this);

            if (!Id.HasValue())
            {
                if (identityProvider == null) throw new NotSupportedException($"No id was provided for colony and no identity provider is available");

                _logger.Debug($"No id provided for colony. Generating name");
                Builder.WithId(identityProvider.GenerateId(this));
                _logger.Debug($"Generated name <{HiveLog.Colony.IdParam}> for colony", Id);
            }
            if (!Name.HasValue()) Name = Id!;
        }

        #region Builder
        /// <inheritdoc/>
        IColonyBuilder IColonyBuilder.WithName(string name)
        {
            lock (_stateLock)
            {
                HiveMindHelper.Validation.ValidateColonyId(name);
                Name = name;
                return this;
            }
        }
        /// <inheritdoc/>
        IColonyBuilder IColonyBuilder.InEnvironment(string environment)
        {
            lock (_stateLock)
            {
                HiveMindHelper.Validation.ValidateEnvironment(environment);
                Environment = environment;
                return this;
            }
        }
        /// <inheritdoc/>
        IColonyBuilder IColonyBuilder.WithOptions(ColonyOptions options)
        {
            lock (_stateLock)
            {
                options.ValidateArgument(nameof(options));
                options.ValidateAgainstProfile<HiveColonyOptionsValidationProfile, ColonyOptions, string>().Errors.ThrowOnValidationErrors(options);

                _options = options;
            }

            return this;
        }
        /// <inheritdoc/>
        IColonyBuilder IColonyConfigurator<IColonyBuilder>.WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder>? builder)
        {
            _ = WithDaemon(name, runDelegate, builder);
            return this;
        }
        /// <inheritdoc/>
        IColonyBuilder IColonyConfigurator<IColonyBuilder>.WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, IDaemonExecutionContext, TInstance>? constructor, bool? allowDispose, Action<IDaemonBuilder>? builder)
        {
            _ = WithDaemon(name, runDelegate, constructor, allowDispose, builder);
            return this;
        }
        IColonyBuilder IColonyBuilder.WithId(string id)
        {
            id = Guard.IsNotNullOrWhitespace(id);
            HiveMindHelper.Validation.ValidateColonyId(id);
            Id = id;
            return this;
        }
        #endregion

        #region Configure
        /// <inheritdoc/>
        public IColonyBuilderConfigurator WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder>? builder)
        {
            lock (_stateLock)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));

                if (_daemons.ContainsKey(name)) throw new InvalidOperationException($"Daemon with name <{name}> already exists");
                var daemon = new Daemon(this, name, null, runDelegate.ValidateArgument(nameof(runDelegate)), builder, _scope.ServiceProvider, _options, _loggerFactory?.CreateLogger($"{typeof(Daemon).FullName}({name})"));

                if(!daemon.Properties.TryGetValue<bool>(HiveMindConstants.Daemon.IsAutoCreatedProperty, out var autoCreated) && !autoCreated)
                {
                    HiveMindHelper.Validation.ValidateDaemonName(name);
                }
                if (Status == ColonyStatus.Running && daemon.AutoStart) daemon.Start();
                _daemons.Add(name, daemon);
            }

            return this;
        }
        /// <inheritdoc/>
        public IColonyBuilderConfigurator WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, IDaemonExecutionContext, TInstance>? constructor, bool? allowDispose, Action<IDaemonBuilder>? builder)
        {
            lock (_stateLock)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));

                if (_daemons.ContainsKey(name)) throw new InvalidOperationException($"Daemon with name <{name}> already exists");
                var daemon = Daemon.FromInstance<TInstance>(this, name, runDelegate.ValidateArgument(nameof(runDelegate)), constructor, allowDispose, builder, _scope.ServiceProvider, _options, _loggerFactory?.CreateLogger($"{typeof(Daemon).FullName}({name})"));
                if (!daemon.Properties.TryGetValue<bool>(HiveMindConstants.Daemon.IsAutoCreatedProperty, out var autoCreated) && !autoCreated)
                {
                    HiveMindHelper.Validation.ValidateDaemonName(name);
                }
                if (Status == ColonyStatus.Running && daemon.AutoStart) daemon.Start();
                _daemons.Add(name, daemon);
            }

            return this;
        }
        /// <inheritdoc/>
        public void Configure(Action<IColonyConfigurator> configure)
        {
            configure = Guard.IsNotNull(configure);

            configure(this);
        }
        /// <inheritdoc/>
        IColonyConfigurator IColonyConfigurator.RemoveDaemon(string name)
        {
            name = Guard.IsNotNullOrWhitespace(name);

            Daemon? daemon;
            lock (_stateLock)
            {
                daemon = _daemons.GetValueOrDefault(name);
            }

            if(daemon == null)
            {
                _logger.Warning($"Could not remove daemon <{HiveLog.Daemon.NameParam}> from colony <{HiveLog.Colony.Id}> because it does not exist", name, Id);
            }
            else
            {
                daemon.MarkedForDeletion = true;
                daemon.Cancel();
            }

            return this;
        }
        /// <inheritdoc/>
        IColonyConfigurator IColonyConfigurator.ChangeName(string name)
        {
            name = Guard.IsNotNullOrWhitespace(name);

            lock (_stateLock)
            {
                Name = name;
            }
            _logger.Log($"Name for colony <{HiveLog.Colony.IdParam}> changed to <{HiveLog.Colony.NameParam}>", Id, Name);

            return this;
        }
        /// <inheritdoc/>
        IColonyConfigurator IColonyConfigurator<IColonyConfigurator>.WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder>? builder)
        {
            _ = WithDaemon(name, runDelegate, builder);
            return this;
        }
        /// <inheritdoc/>
        IColonyConfigurator IColonyConfigurator<IColonyConfigurator>.WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, IDaemonExecutionContext, TInstance>? constructor, bool? allowDispose, Action<IDaemonBuilder>? builder)
        {
            _ = WithDaemon(name, runDelegate, constructor, allowDispose, builder);
            return this;
        }
        #endregion

        #region Manage Daemon
        private async Task ManageDaemonsAsync(bool forceStart, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);
            await using var managementLock = await _managementLock.LockAsync(token).ConfigureAwait(false);

            _logger.Debug($"Managing daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);

            Dictionary<byte, List<Daemon>>? groupedDaemons = null;

            lock (_stateLock)
            {
                groupedDaemons = _daemons.Values.GroupAsDictionary(x => x.Priority, x => x);
            }

            foreach (var (priority, daemons) in groupedDaemons.OrderBy(x => x.Key))
            {
                _logger.Debug($"Checking state of <{daemons.Count}> daemons with a priority of <{priority}>");

                // Check which daemons to start
                List<Daemon> pendingDaemons = new List<Daemon>();
                foreach (var daemon in daemons)
                {
                    lock (daemon.SyncRoot)
                    {
                        // First time starting
                        if (daemon.Status == DaemonStatus.Timedout)
                        {
                            _logger.Warning($"Daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> timed out. Task needs to stop before daemon can be started again", daemon.Name, Name, Environment);
                        }
                        else if (daemon.MarkedForDeletion && daemon.Status.In(DaemonStatus.Stopped, DaemonStatus.Finished, DaemonStatus.FailedToStart, DaemonStatus.Faulted, DaemonStatus.FailedToStop))
                        {
                            _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> is marked for deletion. Removing", daemon.Name, Name, Environment);
                            lock (_stateLock)
                            {
                                _daemons.Remove(daemon.Name);
                            }

                        }
                        else if (!daemon.WasStarted && daemon.AutoStart)
                        {
                            _logger.Log($"Starting daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> for the first time", daemon.Name, Name, Environment);
                            daemon.Start();
                            pendingDaemons.Add(daemon);
                        }
                        else if (forceStart)
                        {
                            _logger.Log($"Starting daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", daemon.Name, Name, Environment);
                            daemon.Start();
                            pendingDaemons.Add(daemon);
                        }
                        else if ((daemon.RestartPolicy == DaemonRestartPolicy.UnlessStopped && daemon.Status.In(DaemonStatus.Faulted, DaemonStatus.Finished)) ||
                                 (daemon.RestartPolicy == DaemonRestartPolicy.OnFailure && daemon.Status == DaemonStatus.Faulted) ||
                                 (daemon.RestartPolicy == DaemonRestartPolicy.Always && !daemon.Status.In(DaemonStatus.Running, DaemonStatus.Starting, DaemonStatus.Stopping)))
                        {
                            _logger.Warning($"Restart policy for daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> is <{daemon.RestartPolicy}> while status is <{daemon.Status}>. Triggering start", daemon.Name, Name, Environment);
                            daemon.Start();
                            pendingDaemons.Add(daemon);
                        }
                    }

                    if(daemon.MarkedForDeletion) await daemon.DisposeAsync().ConfigureAwait(false);
                }

                // Wait for daemons to start
                if (pendingDaemons.HasValue())
                {
                    _logger.Debug($"Waiting for <{pendingDaemons.Count}> daemons in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> to start", Name, Environment);

                    foreach (var daemon in pendingDaemons)
                    {
                        _logger.Log($"Waiting for daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> to start", daemon.Name, Name, Environment);
                        await daemon.WaitUntilRunning(token).ConfigureAwait(false);
                        _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> started", daemon.Name, Name, Environment);
                    }
                }
            }

            _logger.Debug($"Managed daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);
        }

        private async Task ManageDaemonsUntilCancellationAsync(CancellationToken token)
        {
            ColonyOptions options;

            lock (_stateLock)
            {
                options = _options;
            }

            _logger.Log($"Managing daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> every <{options.DaemonManagementInterval}>", Name, Environment);

            do
            {
                var sleepTime = options.DaemonManagementInterval;
                _logger.Debug($"Managing daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}> in <{sleepTime}>", Name, Environment);

                await Helper.Async.Sleep(sleepTime, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;
                if (!Status.In(ColonyStatus.StartingDaemons, ColonyStatus.FailedToStartDaemons, ColonyStatus.Running)) break;

                try
                {
                    await ManageDaemonsAsync(false, token).ConfigureAwait(false);

                    lock (_stateLock)
                    {
                        options = _options;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log($"Something went wrong while managing daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Name, Environment);
                }
            }
            while (!token.IsCancellationRequested);

            _logger.Log($"Stopping the management of daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);
        }

        private async Task<Exception[]> StopDaemonsAsync(CancellationToken token)
        {
            var exceptions = new List<Exception>();
            _logger.Debug($"Stopping daemons for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);
            Dictionary<byte, List<Daemon>>? groupedDaemons = null;

            lock (_stateLock)
            {
                groupedDaemons = _daemons.Values.GroupAsDictionary(x => x.Priority, x => x);
            }

            foreach (var (priority, daemons) in groupedDaemons.OrderByDescending(x => x.Key))
            {
                _logger.Debug($"Stopping <{daemons.Count}> daemons with a priority of <{priority}>");

                // Send stop signal
                daemons.Execute(x => x.Cancel());

                foreach (var daemon in daemons)
                {
                    try
                    {
                        _logger.Log($"Waiting for daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}> to stop", daemon.Name, Id, Environment);
                        await daemon.StopAndWaitAsync(token).ConfigureAwait(false);
                        _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}> stopped gracefully", daemon.Name, Id, Environment);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Something went wrong while stopping daemon <{HiveLog.Daemon.NameParam}> in colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, daemon.Name, Id, Environment);
                        exceptions.Add(ex);
                    }
                }
            }

            return exceptions.ToArray();
        }
        #endregion

        public async Task<IEnumerable<Exception>?> ManageColonyUntilCancellationAsync(CancellationToken token)
        {
            using var logScope = _logger.TryBeginScope(x => x.AddFluently(HiveLog.Environment, Environment).AddFluently(HiveLog.Colony.Id, Id).AddFluently(HiveLog.Colony.Name, Name));

            _logger.Log($"Colony management task started. Waiting for lock");

            Exception[]? daemonExceptions = null;
            var releaseSource = new CancellationTokenSource();
            using var daemonTokenRegistration = token.Register(() => releaseSource.CancelAfter(_options.ReleaseLockTime));

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await ChangeStatusAsync(ColonyStatus.WaitingForLock, new ColonyStatus[] { ColonyStatus.Starting, ColonyStatus.WaitingForLock, ColonyStatus.FailedToLock, ColonyStatus.FailedToStartDaemons, ColonyStatus.LostLock }, token);
                        _logger.Log($"Colony trying to acquire processing lock");
                        // Wait for lock
                        try
                        {
                            while (IsExpired)
                            {
                                token.ThrowIfCancellationRequested();
                                var (wasLocked, lockInfo) = await _colonyService.TrySyncAndGetProcessLockAsync(this, _requester, token).ConfigureAwait(false);
                                lock (_stateLock)
                                {
                                    _hasLock = wasLocked;
                                    Lock = lockInfo;
                                }
                                if (IsExpired)
                                {
                                    _logger.Warning($"Colony failed to acquire processing lock because it is locked by <{lockInfo?.LockedBy}>. Waiting <{HiveOptions.LockTimeout}> before trying again");
                                    await Helper.Async.Sleep(HiveOptions.LockTimeout, token).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Colony management task ran into issue while trying to acquire the state lock", ex);
                            await ChangeStatusAsync(ColonyStatus.FailedToLock, null, releaseSource.Token).ConfigureAwait(false);
                            throw;
                        }

                        // Start lock maintainer
                        _logger.Log($"Colony acquired processing lock. Starting daemons");
                        using var cancellationSource = new CancellationTokenSource();
                        using var tokenRegistration = token.Register(cancellationSource.Cancel);
                        await using var lockMaintainer = ((ILockable)this).KeepAliveDuringScope(this, LockMaintainerTaskName, _taskManager, HiveOptions.LockExpirySafetyOffset, () =>
                        {
                            cancellationSource.Cancel();
                            _logger.Warning($"Colony <{HiveLog.Colony.IdParam}> could not maintain heartbeat on processing lock. Stopping");
                            return Task.CompletedTask;
                        }, _logger, token).ConfigureAwait(false);

                        _logger.Log($"Colony started lock maintainer. Starting state sync task");
                        token.ThrowIfCancellationRequested();

                        // Start sync task
                        using var stateSyncTask = await _taskManager.ScheduleRecurringActionAsync(this, StateSyncTaskName, false, _options.StateSyncInterval, async t =>
                        {
                            _logger.Debug($"Gathering logs from daemons to start colony sync to storage");
                            var logs = GetNewDaemonLogs();

                            _logger.Debug($"Syncing colony state");
                            try
                            {
                                if (await _colonyService.TrySyncStateAsync(this, logs, _requester, t).ConfigureAwait(false))
                                {
                                    _logger.Log($"Colony state synced successfully");
                                }
                                else
                                {
                                    _logger.Warning($"Colony state sync failed. Lock might be stale");
                                    RestoreDaemonLogs(logs);
                                }
                            }
                            catch (Exception)
                            {
                                RestoreDaemonLogs(logs);
                                throw;
                            }
                        }, (e, t) =>
                        {
                            _logger.Log($"Something went wrong while persisting colony state", e);
                            return Task.FromResult(true);
                        }, x => x.WithPolicy(NamedManagedTaskPolicy.CancelAndStart).WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepRunning), false, cancellationSource.Token).ConfigureAwait(false);

                        // Start daemons
                        _logger.Log($"Colony started sync task. Starting daemons");
                        await ChangeStatusAsync(ColonyStatus.StartingDaemons, new ColonyStatus[] { ColonyStatus.WaitingForLock, ColonyStatus.FailedToLock, ColonyStatus.FailedToStartDaemons }, releaseSource.Token).ConfigureAwait(false);
                        try
                        {
                            daemonExceptions = null;
                            await ManageDaemonsAsync(true, cancellationSource.Token).ConfigureAwait(false);
                        }
                        catch (Exception daemonEx) when (!Daemons.Any(x => x.IsRunning || x.IsPending))
                        {
                            _logger.Log($"Colony wasn't able to start any of it's daemons", daemonEx);
                            await ChangeStatusAsync(ColonyStatus.FailedToStartDaemons, null, releaseSource.Token).ConfigureAwait(false);
                            throw;
                        }

                        cancellationSource.Token.ThrowIfCancellationRequested();
                        _logger.Log($"Colony started <{Daemons.Count(x => x.IsRunning || x.IsPending)}> daemons. Starting management task for daemons and sleeping until cancellation");

                        // Running
                        using var daemonManagementTask = await _taskManager.ScheduleActionAsync(this, DaemonManagerTaskName, false, ManageDaemonsUntilCancellationAsync, x => x.WithPolicy(NamedManagedTaskPolicy.CancelAndStart).WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepRunning), cancellationSource.Token).ConfigureAwait(false);
                        await ChangeStatusAsync(ColonyStatus.Running, new ColonyStatus[] { ColonyStatus.StartingDaemons }, releaseSource.Token).ConfigureAwait(false);
                        await Helper.Async.WaitUntilCancellation(cancellationSource.Token).ConfigureAwait(false);

                        var status = token.IsCancellationRequested ? ColonyStatus.Stopping : ColonyStatus.LostLock;
                        using var stopSource = new CancellationTokenSource(_options.DaemonMaxStopTime);
                        var stopDaemonTask = StopDaemonsAsync(stopSource.Token);

                        try
                        {
                            await ChangeStatusAsync(status, new ColonyStatus[] { ColonyStatus.Running, ColonyStatus.Stopping }, releaseSource.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Something went wrong while changing status to <{status}>", ex);
                        }
                        finally
                        {
                            daemonExceptions = await stopDaemonTask.ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return daemonExceptions;
                    }
                    catch (Exception) when (Status.In(ColonyStatus.FailedToLock, ColonyStatus.FailedToStartDaemons, ColonyStatus.LostLock))
                    {
                        _logger.Debug($"Colony management task ran into a known error. Restarting in <{Options.ErrorRetryDelay}>");
                    }
                    catch (Exception ex) when (token.IsCancellationRequested)
                    {
                        _logger.Log($"Colony management task ran into error while cancelling>", ex);
                        return daemonExceptions;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Colony management task ran into error. Restarting in <{Options.ErrorRetryDelay}>", ex);
                    }
                    await Helper.Async.Sleep(Options.ErrorRetryDelay, token).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    await ChangeStatusAsync(ColonyStatus.Stopped, new ColonyStatus[] { ColonyStatus.Running, ColonyStatus.FailedToLock, ColonyStatus.FailedToStartDaemons, ColonyStatus.LostLock, ColonyStatus.Stopping }, token).ConfigureAwait(false);
                }
                finally
                {
                    if (!IsExpired)
                    {
                        _logger.Debug($"Releasing process lock on colony if it is still held by <{_requester}>");
                        await _colonyService.ReleaseLockAndSyncStateIfHeldByAsync(this, GetNewDaemonLogs(), _requester, releaseSource.Token).ConfigureAwait(false);
                    }
                }               
            }
            
            return daemonExceptions;
        }

        private IReadOnlyDictionary<string, IEnumerable<LogEntry>> GetNewDaemonLogs()
        {
            var logs = new Dictionary<string, IEnumerable<LogEntry>>();
            Daemon[]? daemons = null;
            lock (_stateLock)
            {
                daemons = _daemons.Values.ToArray();
            }
            daemons.Execute(x =>
            {
                var entries = x.FlushBuffer();
                if (entries.HasValue()) logs.Add(x.Name, entries);
            });

            return logs;
        }

        private void RestoreDaemonLogs(IReadOnlyDictionary<string, IEnumerable<LogEntry>> logs)
        {
            logs.Execute(x =>
            {
                Daemon? daemon = null;
                lock (_stateLock)
                {
                    daemon = _daemons.GetValueOrDefault(x.Key);
                }

                if (daemon != null)
                {
                    daemon.RestoreBuffer(x.Value);
                }
            });
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken token = default)
        {
            await using (await _managementLock.LockAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await ChangeStatusAsync(ColonyStatus.Starting, new ColonyStatus[] { ColonyStatus.Stopped, ColonyStatus.FailedToStart, ColonyStatus.FailedToStop }, token).ConfigureAwait(false);
                    _logger.Log($"Starting colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);

                    // Start tasks
                    _logger.Debug($"Starting management tasks for colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);
                    await _taskManager.ScheduleActionAsync(this, ManagementTaskName, false, ManageColonyUntilCancellationAsync, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepRunning).WithPolicy(NamedManagedTaskPolicy.CancelAndStart)).ConfigureAwait(false);

                    _logger.Log($"Started colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);
                }
                catch (Exception ex)
                {
                    await ChangeStatusAsync(ColonyStatus.FailedToStart, null, token).ConfigureAwait(false);
                    _logger.Log($"Something went wrong while starting colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>.", ex, Name, Environment);

                    await EnsureStoppedAsync(token).ConfigureAwait(false);

                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken token = default)
        {
            await using (await _managementLock.LockAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await StopColonyAsync(token).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    _logger.Log($"Could not gracefully stop colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.Environment}>", ex, Id, Environment);
                    await ChangeStatusAsync(ColonyStatus.Stopped, new ColonyStatus[] { ColonyStatus.Stopping }, token).ConfigureAwait(false);
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        async Task<bool> ILockable.TryKeepAliveAsync(CancellationToken token)
        {
            using var logScope = _logger.TryBeginScope(x => x.AddFluently(HiveLog.Environment, Environment).AddFluently(HiveLog.Colony.Id, Id).AddFluently(HiveLog.Colony.Name, Name));

            _logger.Debug($"Trying to set heartbeat of process lock");

            var (wasUpdated, lockInfo) = await _colonyService.TryHeartbeatProcessLockAsync(this, _requester, token).ConfigureAwait(false);

            lock (_stateLock)
            {
                _hasLock = wasUpdated;
                Lock = lockInfo;
            }

            if (!wasUpdated)
            {
                _logger.Warning($"Could not extend process lock on colony{(lockInfo != null ? $". Lock is currently held by <{lockInfo.LockedBy}>" : string.Empty)}");
            }
            else
            {
                _logger.Debug($"Heartbeat on lock was set");
            }

            return wasUpdated;
        }

        void ILockable.OnLockExpired(Func<Task> action) => throw new NotSupportedException();

        private async Task StopColonyAsync(CancellationToken token = default)
        {
            await ChangeStatusAsync(ColonyStatus.Stopping, new ColonyStatus[] { ColonyStatus.Running, ColonyStatus.FailedToStop }, token).ConfigureAwait(false);
            var exceptions = new List<Exception>();
            _logger.Log($"Stopping colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);

            // Get management task and try get daemon exceptions
            try
            {
                var managementTask = _taskManager.GetOwnedBy(this).FirstOrDefault(x => x.Name.EqualsNoCase(ManagementTaskName));

                if(managementTask != null)
                {
                    _logger.Debug($"Stopping management task <{managementTask}> for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);

                    managementTask.Cancel();
                    var daemonExceptions = await managementTask.GetResultAsync<IEnumerable<Exception>>(token).ConfigureAwait(false);
                    if (daemonExceptions.HasValue())
                    {
                        _logger.Log($"Could not gracefully stop all daemons for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", new AggregateException(daemonExceptions), Id, Environment);
                    }
                }
                else
                {
                    await ChangeStatusAsync(ColonyStatus.Stopping, null, token).ConfigureAwait(false);
                }
            }
            catch(Exception ex)
            {
                _logger.Log($"Something went wrong while stopping management task for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Id, Environment);
                exceptions.Add(ex);
            }

            // Cancel all tasks
            try
            {
                _logger.Debug($"Stopping management tasks for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);
                await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log($"Something went wrong while stopping tasks for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Id, Environment);
                exceptions.Add(ex);
            }

            // Stop daemons
            try
            {
                exceptions.AddRange(await StopDaemonsAsync(default).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.Log($"Something went wrong while stopping daemons in colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Id, Environment);
                exceptions.Add(ex);
            }

            await ChangeStatusAsync(ColonyStatus.Stopped, null, token).ConfigureAwait(false);

            _logger.Log($"Stopped colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Environment);
            if (exceptions.HasValue()) throw new AggregateException(exceptions);
        }

        private async Task EnsureStoppedAsync(CancellationToken token = default)
        {
            if (Self.IsRunning || Self.IsPending)
            {
                await StopColonyAsync(token).ConfigureAwait(false);
            }
        }

        private async Task RaiseStatusChangedAsync(CancellationToken token = default)
        {
            try
            {
                _logger.Debug($"Raising status changed event for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Id, Environment);
                await _notifier.RaiseEventAsync(this, new ColonyStatusChangedEvent(this), token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log($"Something went wrong while raising status changed event for colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Id, Environment);
                throw;
            }
        }

        private async Task ChangeStatusAsync(ColonyStatus status, IEnumerable<ColonyStatus>? validStates, CancellationToken token = default)
        {
            await using var stateLock = await _statusLock.LockAsync(token).ConfigureAwait(false);
            lock (_stateLock)
            {
                if(validStates != null && !validStates.Contains(Status)) throw new InvalidOperationException($"Cannot change status of colony <{Id}> in environment <{Environment}> from <{Status}> to <{status}>");
                _logger.Debug($"Changing status of colony <{HiveLog.Job.IdParam}>({HiveLog.Colony.NameParam}) from <{HiveLog.Colony.StatusParam}> to <{status}>", Id, Name, Status);
                Status = status;
            }
            await RaiseStatusChangedAsync(token).ConfigureAwait(false);
            _logger.Debug($"Colony <{HiveLog.Job.IdParam}>({HiveLog.Colony.NameParam}) is now in status <{HiveLog.Colony.StatusParam}>", Id, Name, Status);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            await using (await _managementLock.LockAsync().ConfigureAwait(false))
            {
                if (IsDisposed.HasValue) return;

                using (new ExecutedAction(x => IsDisposed = x))
                {
                    var exceptions = new List<Exception>();
                    try
                    {
                        await EnsureStoppedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }

                    try
                    {
                        await _scope.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }

                    if (exceptions.HasValue()) throw new AggregateException(exceptions);
                }
            }
        }
    }
}
