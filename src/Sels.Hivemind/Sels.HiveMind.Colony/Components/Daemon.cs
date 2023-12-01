using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IDaemon"/>
    public class Daemon : IDaemon, IDaemonBuilder
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, object> _localProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<IServiceProvider, IDaemon, CancellationToken, Task> _runDelegate;
        private readonly object _lock = new object();


        // State
        private DaemonStatus _status;

        private CancellationTokenSource _tokenSource;
        private TaskCompletionSource<object> _startSource;
        private Task _task;

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public ushort Priority { get; set; }
        /// <inheritdoc/>
        public object Instance { get; private set; }
        /// <inheritdoc/>
        public Type InstanceType { get; }
        /// <inheritdoc/>
        public DaemonStatus Status { get { lock (_lock) { return _status; } } private set { lock (_lock) { _status = value; } } }
        /// <inheritdoc/>
        public DaemonRestartPolicy RestartPolicy { get; private set; }
        /// <summary>
        /// If the daemon has been started since it's creation.
        /// </summary>
        public bool WasStarted { get; private set; }

        /// <inheritdoc/>
        public object State { get; set; }
        /// <inheritdoc/>
        object IReadOnlyDaemon.State => State;

        /// <inheritdoc/>
        public object SyncRoot { get; } = new object();
        /// <inheritdoc/>
        public IDictionary<string, object> LocalProperties { get { lock (SyncRoot) { return _localProperties; } } }
        /// <inheritdoc/>
        public IDictionary<string, object> Properties { get { lock (SyncRoot) { return _properties; } } }
        /// <inheritdoc/>
        IReadOnlyDictionary<string, object> IReadOnlyDaemon.LocalProperties { get { lock (SyncRoot) { return _localProperties; } } }
        /// <inheritdoc/>
        IReadOnlyDictionary<string, object> IReadOnlyDaemon.Properties { get { lock (SyncRoot) { return _properties; } } }

        /// <inheritdoc cref="Daemon"/>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="runDelegate">The delegate that will be executed when the daemon is requested to be started</param>
        /// <param name="builder">Optional delegate to configure this instance</param>
        /// <param name="serviceProvider">The service provider that will be used to define the service scope when the daemon is running</param>
        /// <param name="logger">Optional logger for tracing</param>
        public Daemon(string name, Func<IServiceProvider, IDaemon, CancellationToken, Task> runDelegate, Action<IDaemonBuilder> builder, IServiceProvider serviceProvider, ILogger logger)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            _runDelegate = runDelegate.ValidateArgument(nameof(runDelegate));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _logger = logger;

            builder?.Invoke(this);
            Status = DaemonStatus.Stopped;
        }

        /// <summary>
        /// Creates a new instance where the daemon executes an instance of <typeparamref name="TInstance"/>.
        /// </summary>
        /// <typeparam name="TInstance">The type of the instance that is to be executed by the daemon</typeparam>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="runDelegate">The delegate that will be executed when the daemon is requested to be started</param>
        /// <param name="constructor">Optional delegate that creates the instance to execute</param>
        /// <param name="allowDispose">If <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/> needs to be called on <typeparamref name="T"/> if implemented. When set to null disposing will be determined based on the constructor used</param>
        /// <param name="builder">Optional delegate to configure this instance</param>
        /// <param name="serviceProvider">The service provider that will be used to define the service scope when the daemon is running</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <returns><inheritdoc cref="Daemon"/></returns>
        public static Daemon FromInstance<TInstance>(string name, Func<TInstance, IDaemon, CancellationToken, Task> runDelegate, Func<IServiceProvider, TInstance> constructor, bool? allowDispose, Action<IDaemonBuilder> builder, IServiceProvider serviceProvider, ILogger logger)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            runDelegate.ValidateArgument(nameof(runDelegate));
            serviceProvider.ValidateArgument(nameof(serviceProvider));

            Func<IServiceProvider, IDaemon, CancellationToken, Task> executeDelegate = new Func<IServiceProvider, IDaemon, CancellationToken, Task>(async (p, d, t) =>
            {
                TInstance instance = default;
                bool fromServiceContainer = false;

                try
                {
                    // Resolve instance
                    if(constructor != null)
                    {
                        instance = constructor(p);
                    }
                    else
                    {
                        instance = p.GetService<TInstance>();

                        if(instance == null)
                        {
                            instance = ActivatorUtilities.CreateInstance<TInstance>(p);
                        }
                        else
                        {
                            fromServiceContainer = true;
                        }
                    }

                    // Execute instance
                    await runDelegate(instance, d, t);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if((allowDispose.HasValue && allowDispose.Value) || (!allowDispose.HasValue && !fromServiceContainer))
                    {
                        if(instance is IAsyncDisposable asyncDisposable)
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

            return new Daemon(name, executeDelegate, builder, serviceProvider, logger);
        }

        #region Builder
        /// <inheritdoc/>
        IDaemonBuilder IDaemonBuilder.WithPriority(ushort priority)
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
            _logger.Log($"Waiting until daemon <{HiveLog.Daemon.Name}> is running", Name);

            await Helper.Async.WaitOn(_startSource.Task, token).ConfigureAwait(false);
            _logger.Log($"Daemon <{HiveLog.Daemon.Name}> is running", Name);
        }
        /// <inheritdoc/>
        public void Start()
        {
            _logger.Log($"Starting daemon <{HiveLog.Daemon.Name}> if it is not running yet", Name);
            lock (_lock)
            {
                if(Status == DaemonStatus.Stopped)
                {
                    _logger.Log($"Starting daemon <{HiveLog.Daemon.Name}>", Name);

                    if(_startSource != null) _startSource.TrySetResult(true); // Always release
                    _startSource = new TaskCompletionSource<object>();
                    if (_tokenSource != null) _tokenSource.Cancel(); // Awlays cancel just in case
                    _tokenSource = new CancellationTokenSource();

                    Status = DaemonStatus.Starting;
                    _task = Task.Run(() => RunAsync(_tokenSource.Token));
                    WasStarted = true;
                }
                else
                {
                    _logger.Log($"Cannot start daemon <{HiveLog.Daemon.Name}> because it's status is <{Status}>", Name);
                }
            }
        }

        public async Task RunAsync(CancellationToken token = default)
        {
            try
            {
                _logger.Log($"Daemon <{HiveLog.Daemon.Name}> started up. Calling delegate", Name);

                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    lock (_lock)
                    {
                        Status = DaemonStatus.Running;
                        _startSource.TrySetResult(true);
                    }
                    await _runDelegate(scope.ServiceProvider, this, token).ConfigureAwait(false);
                }

                _logger.Log($"Daemon <{HiveLog.Daemon.Name}> stopped running gracefully", Name);
                Status = DaemonStatus.Finished;
            }
            catch (Exception ex)
            {
                _logger.Log($"Daemon <{HiveLog.Daemon.Name}> ran into a fatal exception and will stop running", ex, Name);

                Status = DaemonStatus.Faulted;
            }
            finally
            {
                lock (_lock)
                {
                    _startSource.TrySetResult(false);
                }
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            _logger.Log($"Cancelling daemon <{HiveLog.Daemon.Name}> if it is running", Name);
            lock(_lock)
            {
                if(_tokenSource != null && !_tokenSource.IsCancellationRequested)
                {
                    _tokenSource.Cancel();
                    _logger.Log($"Cancelled daemon <{HiveLog.Daemon.Name}>", Name);
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
            lock(_lock)
            {
                Cancel();

                task = _task;
            }

            if(task != null)
            {
                _logger.Log($"Waiting until daemon <{HiveLog.Daemon.Name}> stops running", Name);
                await Helper.Async.WaitOn(task, token).ConfigureAwait(false);
            }
        }
    }
}
