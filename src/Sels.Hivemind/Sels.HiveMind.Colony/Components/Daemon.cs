using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Colony.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IDaemon"/>
    public class Daemon : IDaemon, IDaemonExecutionContext, IDaemonBuilder, IAsyncDisposable
    {
        // Fields
        private readonly ILogger? _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, object> _localProperties = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, object> _properties = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<IDaemonExecutionContext, CancellationToken, Task> _runDelegate;
        private readonly object _lock = new object();
        private readonly HashSet<LogEntry> _logBuffer = new HashSet<LogEntry>();
        private readonly ColonyOptions _colonyOptions;

        // State
        private DaemonStatus _status;
        private LogLevel? _enabledLogLevel;

        private CancellationTokenSource _tokenSource;
        private TaskCompletionSource<object> _startSource;
        private Task _task;

        // Properties
        /// <inheritdoc/>
        public IColony Colony { get; private set; }
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public byte Priority { get; set; }
        /// <inheritdoc/>
        public object Instance { get; private set; }
        /// <inheritdoc/>
        public Type InstanceType { get; }
        /// <inheritdoc/>
        public DaemonStatus Status { get { lock (_lock) { return _status; } } private set { lock (_lock) { _status = value; } } }
        /// <inheritdoc/>
        public DaemonRestartPolicy RestartPolicy { get; private set; }
        /// <inheritdoc/>
        public LogLevel EnabledLogLevel => _enabledLogLevel ?? _colonyOptions.DefaultDaemonLogLevel;

        /// <summary>
        /// If the daemon has been started since it's creation.
        /// </summary>
        public bool WasStarted { get; private set; }
        /// <summary>
        /// If the daemon is marked for deeltion. This means that the daemon will be removed from the colony once it's stopped.
        /// </summary>
        public bool MarkedForDeletion { get; set; }

        /// <inheritdoc/>
        public object? State => StateGetter?.Invoke();
        /// <inheritdoc/>
        object? IDaemonInfo.State => State;

        /// <inheritdoc/>
        public object SyncRoot { get; } = new object();
        /// <inheritdoc/>
        public IDictionary<string, object> LocalProperties { get { lock (SyncRoot) { return _localProperties; } } }
        /// <inheritdoc/>
        public IDictionary<string, object> Properties { get { lock (SyncRoot) { return _properties; } } }
        /// <inheritdoc/>
        IReadOnlyDictionary<string, object> IReadOnlyDaemon.LocalProperties { get { lock (SyncRoot) { return _localProperties; } } }
        /// <inheritdoc/>
        IReadOnlyDictionary<string, object> IDaemonInfo.Properties { get { lock (SyncRoot) { return _properties; } } }
        /// <inheritdoc/>
        IWriteableDaemon IDaemonExecutionContext.Daemon => this;
        /// <inheritdoc/>
        IServiceProvider IDaemonExecutionContext.ServiceProvider => _serviceProvider;
        /// <inheritdoc/>
        public Func<object?>? StateGetter { get; set; }
        /// <inheritdoc/>
        IReadOnlyColony IReadOnlyDaemon.Colony => Colony;
        /// <inheritdoc/>
        IWriteableColony IWriteableDaemon.Colony => Colony;
        /// <inheritdoc/>
        ConcurrentDictionary<string, object> IWriteableDaemon.LocalProperties { get { lock (SyncRoot) { return _localProperties; } } }
        /// <inheritdoc/>
        ConcurrentDictionary<string, object> IWriteableDaemon.Properties { get { lock (SyncRoot) { return _properties; } } }
        /// <inheritdoc/>
        public bool AutoStart { get; private set; }
        /// <inheritdoc/>
        IColonyInfo IDaemonInfo.Colony => Colony;
        /// <inheritdoc/>
        byte IDaemonInfo.Priority => Priority;

        /// <inheritdoc cref="Daemon"/>
        /// <param name="colony"><inheritdoc cref="Colony"/></param>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="instanceType"><inheritdoc cref="InstanceType"/></param>
        /// <param name="runDelegate">The delegate that will be executed when the daemon is requested to be started</param>
        /// <param name="builder">Optional delegate to configure this instance</param>
        /// <param name="serviceProvider">The service provider that will be used to define the service scope when the daemon is running</param>
        /// <param name="colonyOptions">Used to retrieve the configured options for the colony the daemon is attached to</param>
        /// <param name="logger">Optional logger for tracing</param>
        public Daemon(IColony colony, string name, Type? instanceType, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder>? builder, IServiceProvider serviceProvider, ColonyOptions colonyOptions, ILogger? logger)
        {
            Colony = colony.ValidateArgument(nameof(colony));
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            InstanceType = instanceType;
            _runDelegate = runDelegate.ValidateArgument(nameof(runDelegate));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _colonyOptions = colonyOptions.ValidateArgument(nameof(colonyOptions));
            _logger = logger;

            builder?.Invoke(this);
            Status = DaemonStatus.Stopped;
        }

        /// <summary>
        /// Creates a new instance where the daemon executes an instance of <typeparamref name="TInstance"/>.
        /// </summary>
        /// <typeparam name="TInstance">The type of the instance that is to be executed by the daemon</typeparam>
        /// <param name="colony"><inheritdoc cref="Colony"/></param>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="runDelegate">The delegate that will be executed when the daemon is requested to be started</param>
        /// <param name="constructor">Optional delegate that creates the instance to execute</param>
        /// <param name="allowDispose">If <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/> needs to be called on <typeparamref name="T"/> if implemented. When set to null disposing will be determined based on the constructor used</param>
        /// <param name="builder">Optional delegate to configure this instance</param>
        /// <param name="serviceProvider">The service provider that will be used to define the service scope when the daemon is running</param>
        /// <param name="colonyOptions">Used to retrieve the configured options for the colony the daemon is attached to</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <returns><inheritdoc cref="Daemon"/></returns>
        public static Daemon FromInstance<TInstance>(IColony colony, string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, IDaemonExecutionContext, TInstance>? constructor, bool? allowDispose, Action<IDaemonBuilder>? builder, IServiceProvider serviceProvider, ColonyOptions colonyOptions, ILogger? logger)
        {
            colony.ValidateArgument(nameof(colony));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            runDelegate.ValidateArgument(nameof(runDelegate));
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            colonyOptions.ValidateArgument(nameof(colonyOptions));

            Func<IDaemonExecutionContext, CancellationToken, Task> executeDelegate = new Func<IDaemonExecutionContext, CancellationToken, Task>(async (c, t) =>
            {
                TInstance? instance = default;
                bool fromServiceContainer = false;

                try
                {
                    // Resolve instance
                    if (constructor != null)
                    {
                        instance = constructor(c.ServiceProvider, c);
                    }
                    else
                    {
                        instance = c.ServiceProvider.GetService<TInstance>();

                        if (instance == null)
                        {
                            instance = ActivatorUtilities.CreateInstance<TInstance>(c.ServiceProvider);
                        }
                        else
                        {
                            fromServiceContainer = true;
                        }
                    }

                    // Execute instance
                    await runDelegate(instance, c, t);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if ((allowDispose.HasValue && allowDispose.Value) || (!allowDispose.HasValue && !fromServiceContainer))
                    {
                        if (instance is IAsyncDisposable asyncDisposable)
                        {
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        }
                        else if (instance is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }
            });

            return new Daemon(colony, name, typeof(TInstance), executeDelegate, builder, serviceProvider, colonyOptions, logger);
        }

        #region Builder
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithPriority(byte priority)
        {
            lock (SyncRoot)
            {
                Priority = priority;
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithRestartPolicy(DaemonRestartPolicy restartPolicy)
        {
            lock (SyncRoot)
            {
                RestartPolicy = restartPolicy;
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithLogLevel(LogLevel? logLevel)
        {
            lock (SyncRoot)
            {
                _enabledLogLevel = logLevel;
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithLocalProperty(string name, object value)
        {
            lock (SyncRoot)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));

                _localProperties.AddOrUpdate(name, value);
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithLocalProperties(IEnumerable<KeyValuePair<string, object>> properties)
        {
            lock (SyncRoot)
            {
                properties.ValidateArgument(nameof(properties));

                properties.Execute(x => this.CastTo<IDaemonBuilder>().WithLocalProperty(x.Key, x.Value));
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithProperty(string name, object value)
        {
            lock (SyncRoot)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));

                _properties.AddOrUpdate(name, value);
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithProperties(IEnumerable<KeyValuePair<string, object>> properties)
        {
            lock (SyncRoot)
            {
                properties.ValidateArgument(nameof(properties));

                properties.Execute(x => this.CastTo<IDaemonBuilder>().WithProperty(x.Key, x.Value));
            }
            return this;
        }
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.DisableAutoStart()
        {
            AutoStart = false;
            return this;
        }
        #endregion

        #region Context
        /// <inheritdoc/>
        void IDaemonExecutionContext.Log(LogLevel logLevel, string message, params object[] logParameters)
        {
            message.ValidateArgument(nameof(message));

            _logger.LogMessage(logLevel, message, logParameters);

            if (logLevel >= EnabledLogLevel)
            {
                var logEntry = new LogEntry(logLevel, message, logParameters, null);
                lock (_logBuffer)
                {
                    _logBuffer.Add(logEntry);
                }
            }
        }
        /// <inheritdoc/>
        void IDaemonExecutionContext.Log(LogLevel logLevel, string message, Exception exception, params object[] logParameters)
        {
            message.ValidateArgument(nameof(message));

            _logger.LogMessage(logLevel, message, exception, logParameters);

            if (logLevel >= EnabledLogLevel)
            {
                var logEntry = new LogEntry(logLevel, message, logParameters, exception);
                lock (_logBuffer)
                {
                    _logBuffer.Add(logEntry);
                }
            }
        }

        /// <summary>
        /// Returns any log entries that have been added since the last flush and removes it from the buffer of the daemon.
        /// </summary>
        /// <returns></returns>
        public LogEntry[] FlushBuffer()
        {
            lock (_logBuffer)
            {
                var buffer = _logBuffer.ToArray();
                _logBuffer.Clear();

                return buffer;
            }
        }

        /// <summary>
        /// Adds <paramref name="logEntries"/> to the log buffer of the daemon.
        /// Only called when the logs couldn't be persisted to the storage so we don't lose them.
        /// </summary>
        /// <param name="logEntries">Enumerator containing any logs to restore</param>
        public void RestoreBuffer(IEnumerable<LogEntry> logEntries)
        {
            logEntries = Guard.IsNotNull(logEntries);

            lock (_logBuffer)
            {
                _logBuffer.IntersectWith(logEntries);
            }
        }
        #endregion

        /// <summary>
        /// Starts the daemon if it's not running yet and waits until it's running.
        /// </summary>
        /// <param name="token">Optional token that can be cancelled to stop the waiting</param>
        /// <returns>Task containing the execution state</returns>
        public async Task StartAndWaitAsync(CancellationToken token = default)
        {
            Start();

            await WaitUntilRunning(token).ConfigureAwait(false);
        }
        /// <summary>
        /// Will wait until the daemon is running asynchronously.
        /// </summary>
        /// <param name="token">Optional token that can be cancelled to stop the waiting</param>
        /// <returns>Task containing the execution state</returns>
        public async Task WaitUntilRunning(CancellationToken token = default)
        {
            _logger.Log($"Waiting until daemon <{HiveLog.Daemon.NameParam}> is running", Name);

            await Helper.Async.WaitOn(_startSource.Task, token).ConfigureAwait(false);
            _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> is running", Name);
        }
        /// <inheritdoc/>
        public void Start()
        {
            _logger.Log($"Starting daemon <{HiveLog.Daemon.NameParam}> if it is not running yet", Name);
            lock (_lock)
            {
                if (MarkedForDeletion) throw new InvalidOperationException("Cannot start daemon because it has been marked for deletion");
                if (Status.In(DaemonStatus.Stopped, DaemonStatus.Faulted, DaemonStatus.Finished))
                {
                    _logger.Log($"Starting daemon <{HiveLog.Daemon.NameParam}>", Name);

                    if (_startSource != null) _startSource.TrySetResult(true); // Always release
                    _startSource = new TaskCompletionSource<object>();
                    if (_tokenSource != null) _tokenSource.Cancel(); // Always cancel just in case
                    _tokenSource = new CancellationTokenSource();

                    Status = DaemonStatus.Starting;
                    _task = Task.Run(() => RunAsync(_tokenSource.Token));
                    WasStarted = true;
                }
                else
                {
                    _logger.Log($"Cannot start daemon <{HiveLog.Daemon.NameParam}> because it's status is <{Status}>", Name);
                }
            }
        }
        private async Task RunAsync(CancellationToken token = default)
        {
            using var logScope = _logger.TryBeginScope(this);

            try
            {
                _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> started up. Calling delegate", Name);

                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    lock (_lock)
                    {
                        Status = DaemonStatus.Running;
                        _startSource.TrySetResult(true);
                    }
                    await _runDelegate(this, token).ConfigureAwait(false);
                }

                _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> stopped running gracefully", Name);
                Status = DaemonStatus.Finished;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.Warning($"Daemon <{HiveLog.Daemon.NameParam}> was cancelled", Name);
                Status = DaemonStatus.Finished;
            }
            catch (Exception ex) when (token.IsCancellationRequested)
            {
                _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> ran into a fatal exception while stopping", ex, Name);

                Status = DaemonStatus.FailedToStop;
            }
            catch (Exception ex)
            {
                _logger.Log($"Daemon <{HiveLog.Daemon.NameParam}> ran into a fatal exception and will stop running", ex, Name);

                Status = DaemonStatus.Faulted;
            }
            finally
            {
                lock (_lock)
                {
                    StateGetter = null;
                    _startSource.TrySetResult(false);
                }
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            _logger.Log($"Cancelling daemon <{HiveLog.Daemon.NameParam}> if it is running", Name);
            lock (_lock)
            {
                if (_tokenSource != null && !_tokenSource.IsCancellationRequested)
                {
                    _tokenSource.Cancel();
                    _logger.Log($"Cancelled daemon <{HiveLog.Daemon.NameParam}>", Name);
                }
            }
        }

        /// <summary>
        /// Stops the daemon if it's running and waits until it is fully stopped.
        /// </summary>
        /// <param name="token">Optional token to cancel the stop if not initiated yet</param>
        /// <returns>Task containing the execution state</returns>
        public async Task StopAndWaitAsync(CancellationToken token = default)
        {
            Task task;
            lock (_lock)
            {
                Cancel();

                task = _task;
            }

            if (task != null)
            {
                _logger.Log($"Waiting until daemon <{HiveLog.Daemon.NameParam}> stops running", Name);
                await Helper.Async.WaitOn(task, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _logger.Debug($"Disposing daemon <{HiveLog.Daemon.NameParam}> from colony <{HiveLog.Colony.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Name, Colony.Id, Colony.Environment);

            await StopAndWaitAsync().ConfigureAwait(false);
        }
    }
}
