using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Dispose;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Threading;
using Sels.Core.Extensions.Validation;
using Sels.Core.Mediator;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Colony.Events;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IColony"/>
    public class HiveColony : IColony, IColonyBuilder, IAsyncExposedDisposable
    {
        // Fieds
        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _managementLock = new SemaphoreSlim(1, 1);
        private readonly INotifier _notifier;
        private readonly AsyncServiceScope _scope;
        private readonly ITaskManager _taskManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        // State
        private readonly Dictionary<string, Daemon> _daemons = new Dictionary<string, Daemon>();
        private HiveColonyOptions _options;

        // Properties
        /// <inheritdoc/>
        IReadOnlyList<IDaemon> IColony.Daemons { get { lock (_stateLock) { return _daemons.Values.ToList(); } } }
        /// <inheritdoc/>
        IReadOnlyList<IReadOnlyDaemon> IReadOnlyColony.Daemons { get { lock (_stateLock) { return _daemons.Values.ToList(); } } }
        /// <inheritdoc/>
        IReadOnlyColony IColonyBuilder.Current => this;
        /// <inheritdoc/>
        public Guid Id { get; } = Guid.NewGuid();
        /// <inheritdoc/>
        public string Name { get; private set; }
        /// <inheritdoc/>
        public string Environment { get; private set; }
        /// <inheritdoc/>
        public ColonyStatus Status { get; private set; }
        /// <inheritdoc/>
        public IColonyOptions Options => _options;
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }


        /// <inheritdoc cref="HiveColony"/>
        /// <param name="configurator">Delegate that configures this instance</param>
        /// <param name="serviceScope">The service scope that shares the same lifetime as the colony. Used to resolve dependencies</param>
        /// <param name="notifier">Used to raise events</param>
        /// <param name="taskManager">Used to manage background tasks</param>
        /// <param name="identityProvider">Used to generate a unique name for this instance if <paramref name="configurator"/> does not provide one</param>
        /// <param name="loggerFactory">Optional logger factory for creating loggers for the daemons</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveColony(Action<IColonyBuilder> configurator, AsyncServiceScope serviceScope, INotifier notifier, ITaskManager taskManager, IColonyIdentityProvider identityProvider = null, ILoggerFactory loggerFactory = null, ILogger<HiveColony> logger = null)
        {
            configurator.ValidateArgument(nameof(configurator));
            identityProvider.ValidateArgument(nameof(identityProvider));
            _scope = serviceScope;
            _notifier = notifier.ValidateArgument(nameof(notifier));
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));
            _loggerFactory = loggerFactory;
            _logger = logger;

            this.CastTo<IColonyBuilder>().InEnvironment(HiveMindConstants.DefaultEnvironmentName);
            this.CastTo<IColonyBuilder>().WithOptions(new HiveColonyOptions());
            configurator(this);

            if (!Name.HasValue())
            {
                if (identityProvider == null) throw new NotSupportedException($"No name was provided for colony and no identity provider is available");

                _logger.Debug($"No name provided for colony. Generating name");
                this.CastTo<IColonyBuilder>().WithName(identityProvider.GenerateName(this));
                _logger.Debug($"Generated name <{HiveLog.Colony.Name}> for colony", Name);
            }
        }

        #region Builder
        /// <inheritdoc/>
        IColonyBuilder IColonyBuilder.WithName(string name)
        {
            lock (_stateLock)
            {
                HiveMindHelper.Validation.ValidateColonyName(name);
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
        IColonyBuilder IColonyBuilder.WithOptions(HiveColonyOptions options)
        {
            lock (_stateLock)
            {
                options.ValidateArgument(nameof(options));
                options.ValidateAgainstProfile<HiveColonyOptionsValidationProfile, HiveColonyOptions, string>().Errors.ThrowOnValidationErrors(options);

                _options = options;
            }

            return this;
        }
        /// <inheritdoc/>
        IColonyBuilder IColonyConfigurator<IColonyBuilder>.WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder> builder)
        {
            _ = WithDaemon(name, runDelegate, builder);
            return this;
        }
        /// <inheritdoc/>
        IColonyBuilder IColonyConfigurator<IColonyBuilder>.WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, TInstance> constructor, bool? allowDispose, Action<IDaemonBuilder> builder)
        {
            _ = WithDaemon(name, runDelegate, constructor, allowDispose, builder);
            return this;
        }
        #endregion

        #region Configure
        /// <inheritdoc/>
        public IColonyConfigurator WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder> builder)
        {
            lock (_stateLock)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));

                if (_daemons.ContainsKey(name)) throw new InvalidOperationException($"Daemon with name <{name}> already exists");

                _daemons.Add(name, new Daemon(this, name, runDelegate.ValidateArgument(nameof(runDelegate)), builder, _scope.ServiceProvider, _options, _loggerFactory?.CreateLogger($"{typeof(Daemon).FullName}({name})")));
            }

            return this;
        }
        /// <inheritdoc/>
        public IColonyConfigurator WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, TInstance> constructor, bool? allowDispose, Action<IDaemonBuilder> builder)
        {
            lock (_stateLock)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));

                if (_daemons.ContainsKey(name)) throw new InvalidOperationException($"Daemon with name <{name}> already exists");

                _daemons.Add(name, Daemon.FromInstance<TInstance>(this, name, runDelegate.ValidateArgument(nameof(runDelegate)), constructor, allowDispose, builder, _scope.ServiceProvider, _options, _loggerFactory?.CreateLogger($"{typeof(Daemon).FullName}({name})")));
            }

            return this;
        }
        #endregion

        #region Manage Daemon
        private async Task ManageDaemons(bool forceStart, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);

            _logger.Debug($"Managing daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);

            Dictionary<ushort, List<Daemon>> groupedDaemons = null;

            lock(_stateLock)
            {
                groupedDaemons = _daemons.Values.GroupAsDictionary(x => x.Priority, x => x);
            }

            foreach(var (priority, daemons) in groupedDaemons.OrderBy(x => x.Key))
            {
                _logger.Debug($"Checking state of <{daemons.Count}> daemons with a priority of <{priority}>");

                // Check which daemons to start
                List<Daemon> pendingDaemons = new List<Daemon>();
                foreach(var daemon in daemons)
                {
                    lock (daemon.SyncRoot)
                    {
                        // First time starting
                        if (!daemon.WasStarted)
                        {
                            _logger.Log($"Starting daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> for the first time", daemon.Name, Name, Environment);
                            daemon.Start();
                            pendingDaemons.Add(daemon);
                        }
                        else if (forceStart)
                        {
                            _logger.Log($"Starting daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", daemon.Name, Name, Environment);
                            daemon.Start();
                            pendingDaemons.Add(daemon);
                        }
                        else if ((daemon.RestartPolicy == DaemonRestartPolicy.UnlessStopped && daemon.Status.In(DaemonStatus.Faulted, DaemonStatus.Finished)) || 
                                 (daemon.RestartPolicy == DaemonRestartPolicy.OnFailure && daemon.Status == DaemonStatus.Faulted) || 
                                 (daemon.RestartPolicy == DaemonRestartPolicy.Always && !daemon.Status.In(DaemonStatus.Running, DaemonStatus.Starting, DaemonStatus.Stopping)))
                        {
                            _logger.Warning($"Restart policy for daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> is <{daemon.RestartPolicy}> while status is <{daemon.Status}>. Triggering start", daemon.Name, Name, Environment);
                            daemon.Start();
                            pendingDaemons.Add(daemon);
                        }
                    }                   
                }

                // Wait for daemons to start
                if (pendingDaemons.HasValue())
                {
                    _logger.Debug($"Waiting for <{pendingDaemons.Count}> daemons in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> to start", Name, Environment);

                    foreach(var daemon in pendingDaemons)
                    {
                        _logger.Log($"Waiting for daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> to start", daemon.Name, Name, Environment);
                        await daemon.WaitUntilRunning(token).ConfigureAwait(false);
                        _logger.Log($"Daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> started", daemon.Name, Name, Environment);
                    }
                }
            }

            _logger.Debug($"Managed daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
        }

        private async Task ManageDaemonsUntilCancellation(CancellationToken token)
        {
            HiveColonyOptions options;

            lock (_stateLock)
            {
                options = _options;
            }

            _logger.Log($"Managing daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> every <{options.DaemonManagementInterval}>", Name, Environment);

            do
            {
                var sleepTime = options.DaemonManagementInterval;
                _logger.Debug($"Managing daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> in <{sleepTime}>", Name, Environment);

                await Helper.Async.Sleep(sleepTime, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                try
                {
                    await using (await _managementLock.LockAsync(token).ConfigureAwait(false))
                    {
                        await ManageDaemons(false, token).ConfigureAwait(false);
                    }

                    lock (_stateLock)
                    {
                        options = _options;
                    }
                }
                catch(OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log($"Something went wrong while managing daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", ex, Name, Environment);
                }
            }
            while(!token.IsCancellationRequested);

            _logger.Log($"Stopping the management of daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
        }

        private async Task<Exception[]> StopDaemons()
        {
            var exceptions = new List<Exception>();
            _logger.Debug($"Stopping daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
            Dictionary<ushort, List<Daemon>> groupedDaemons = null;

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
                        _logger.Log($"Waiting for daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> to stop", daemon.Name, Name, Environment);
                        await daemon.StopAndWaitAsync().ConfigureAwait(false);
                        _logger.Log($"Daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}> stopped gracefully", daemon.Name, Name, Environment);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Something went wrong while stopping daemon <{HiveLog.Daemon.Name}> in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", ex, daemon.Name, Name, Environment);
                        exceptions.Add(ex);
                    }
                }
            }

            return exceptions.ToArray();
        }
        #endregion

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken token = default)
        {
            try
            {
                await using (await _managementLock.LockAsync(token).ConfigureAwait(false))
                {
                    lock (_stateLock)
                    {
                        if (!Status.In(ColonyStatus.Stopped, ColonyStatus.Faulted)) throw new InvalidOperationException($"Cannot start colony <{Name}> in environment <{Environment}> because it's status is <{Status}>");
                        Status = ColonyStatus.Starting;
                    }
                    await RaiseStatusChanged(token).ConfigureAwait(false);
                    _logger.Log($"Starting colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);

                    // Start tasks
                    _logger.Debug($"Starting management tasks for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
                    await _taskManager.ScheduleActionAsync(this, nameof(ManageDaemonsUntilCancellation), false, ManageDaemonsUntilCancellation, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepRunning)).ConfigureAwait(false);

                    // Start daemons
                    _logger.Debug($"Starting daemons for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
                    await ManageDaemons(true).ConfigureAwait(false);

                    lock (_stateLock)
                    {
                        Status = ColonyStatus.Running;
                    }
                    await RaiseStatusChanged(token).ConfigureAwait(false);
                    _logger.Log($"Started colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
                }
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    Status = ColonyStatus.Faulted;
                }
                await RaiseStatusChanged(token).ConfigureAwait(false);
                _logger.Log($"Something went wrong while starting colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>.", ex, Name, Environment);

                await StopAsync().ConfigureAwait(false);

                throw;
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken token = default)
        {
            await using (await _managementLock.LockAsync(token).ConfigureAwait(false))
            {
                await StopColonyAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task StopColonyAsync(CancellationToken token = default)
        {
            lock (_stateLock)
            {
                if (!Status.In(ColonyStatus.Running, ColonyStatus.Faulted)) throw new InvalidOperationException($"Cannot stop colony <{Name}> in environment <{Environment}> because it's status is <{Status}>");
                Status = ColonyStatus.Stopping;
            }
            await RaiseStatusChanged(token).ConfigureAwait(false);
            var exceptions = new List<Exception>();
            _logger.Log($"Stopping colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);

            // Cancel tasks
            try
            {
                _logger.Debug($"Stopping management tasks for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
                await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log($"Something went wrong while stopping tasks for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", ex, Name, Environment);
                exceptions.Add(ex);
            }

            // Stop daemons
            try
            {
                exceptions.AddRange(await StopDaemons().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.Log($"Something went wrong while stopping daemons in colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", ex, Name, Environment);
                exceptions.Add(ex);
            }

            lock (_stateLock)
            {
                Status = ColonyStatus.Stopped;
            }
            await RaiseStatusChanged(token).ConfigureAwait(false);
            _logger.Log($"Stopped colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
            if (exceptions.HasValue()) throw new AggregateException(exceptions);
        }

        private async Task RaiseStatusChanged(CancellationToken token = default)
        {
            try
            {
                _logger.Debug($"Raising status changed event for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", Name, Environment);
                await _notifier.RaiseEventAsync(this, new ColonyStatusChangedEvent(this), token).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                _logger.Log($"Something went wrong while raising status changed event for colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", ex, Name, Environment);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            await using (await _managementLock.LockAsync().ConfigureAwait(false))
            {
                if (IsDisposed.HasValue) return;

                using(new ExecutedAction(x => IsDisposed = x))
                {
                    var exceptions = new List<Exception>();
                    try
                    {
                        await StopColonyAsync().ConfigureAwait(false);
                    }
                    catch(Exception ex)
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
