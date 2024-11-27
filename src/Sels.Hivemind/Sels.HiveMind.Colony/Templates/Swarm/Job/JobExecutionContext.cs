using Sels.Core.Async.Queue;
using Sels.Core.Async.TaskManagement;
using Sels.HiveMind.Colony.Swarm.Job.Background;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Job;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sels.Core.Dispose;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Threading;
using Sels.Core.Scope.Actions;

namespace Sels.HiveMind.Colony.Templates.Swarm.Job
{
    /// <summary>
    /// Base class for creating job execution contexts.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the drone swarm executing the job</typeparam>
    /// <typeparam name="TJob">The type of job being executed</typeparam>
    /// <typeparam name="TAction">The type of actions that can be executed on <see cref="Job"/></typeparam>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    /// <typeparam name="TStateStorageData">The type of storage data used to store the job states</typeparam>
    /// <typeparam name="TStorageData">The type of storage data used by the job</typeparam>
    public abstract class JobExecutionContext<TOptions, TJob, TAction, TStorageData, TState, TStateStorageData> : IJobExecutionContext<TJob>, IAsyncExposedDisposable
        where TJob : IReadOnlyJob
    {
        // Fields
        private readonly ILogger? _jobLogger;
        private readonly IStorage _storage;
        private readonly ITaskManager _taskManager;
        private readonly IJobService<TStorageData, TState, TStateStorageData> _service;
        private readonly IActivatorScope _activator;
        private readonly IDaemonExecutionContext _daemonContext;
        private readonly IDroneState<TOptions> _droneState;
        private readonly LogLevel _enabledLevel;
        private readonly TimeSpan _logFlushInterval;
        private readonly TimeSpan _actionInterval;
        private readonly int _actionFetchLimit;
        private readonly IDelayedPendingTask<IManagedTask> _pendingLogFlusherTask;
        private readonly IDelayedPendingTask<IManagedTask> _actionQueueCreatorTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource _jobCancellationSource;
        private readonly SemaphoreSlim _logLock = new SemaphoreSlim(1, 1);
        private readonly HashSet<LogEntry> _logBuffer = new HashSet<LogEntry>();

        // State
        private WorkerQueue<ActionInfo> _pendingActionQueue;

        // Properties
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }
        /// <inheritdoc/>
        public TJob Job { get; }
        /// <inheritdoc/>
        public string Colony => _daemonContext.Daemon.Colony.Name;
        /// <inheritdoc/>
        public string Swarm => _droneState.Swarm.Name;
        /// <inheritdoc/>
        public string Drone => _droneState.Name;
        /// <inheritdoc/>
        public object? JobInstance { get; }
        /// <inheritdoc/>
        public object[] InvocationArguments { get; }
        /// <inheritdoc/>
        public TimeSpan Duration { get; set; }
        /// <inheritdoc/>
        public object? Result { get; set; }

        /// <inheritdoc cref="JobExecutionContext{TOptions, TJob, TAction, TStorageData, TState, TStateStorageData}"/>
        /// <param name="daemonContext">The context of the daemon currently executing the job</param>
        /// <param name="droneState">The state of the drone executing <paramref name="job"/></param>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="jobInstance"><inheritdoc cref="JobInstance"/></param>
        /// <param name="invocationArguments"><inheritdoc cref="InvocationArguments"/></param>
        /// <param name="enabledLogLevel">The log level above which to persist logs created by the executing job</param>
        /// <param name="logFlushInterval">How often to flush logs to storage</param>
        /// <param name="storage">The storage to use to persist state</param>
        /// <param name="taskManager">The task manager to use to manage recurring tasks</param>
        /// <param name="service">Used to fetch action for the executing job</param>
        /// <param name="actionFetchLimit">How many actions can be pulled into memory at the same time</param>
        /// <param name="actionInterval">How often to check for pending actions for the job</param>
        /// <param name="activator">Used to activate pending actions</param>
        /// <param name="jobCancellationSource">Used to cancel the running job</param>
        /// <param name="loggerFactory">Used to create the ILogger to use to trace logs created by the job</param>
        public JobExecutionContext(IDaemonExecutionContext daemonContext, IDroneState<TOptions> droneState, TJob job, object? jobInstance, object[] invocationArguments, CancellationTokenSource jobCancellationSource, LogLevel enabledLogLevel, TimeSpan logFlushInterval, TimeSpan actionInterval, int actionFetchLimit, IJobService<TStorageData, TState, TStateStorageData> service, IActivatorScope activator, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory)
        {
            _daemonContext = daemonContext.ValidateArgument(nameof(daemonContext));
            _droneState = droneState.ValidateArgument(nameof(droneState));
            Job = job.ValidateArgument(nameof(job));
            JobInstance = jobInstance;
            InvocationArguments = invocationArguments.ValidateArgument(nameof(invocationArguments));
            _jobCancellationSource = jobCancellationSource.ValidateArgument(nameof(jobCancellationSource));
            _enabledLevel = enabledLogLevel;
            _activator = activator.ValidateArgument(nameof(activator));
            _service = service.ValidateArgument(nameof(service));
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));
            _storage = storage.ValidateArgument(nameof(storage));
            _logFlushInterval = logFlushInterval;
            _actionInterval = actionInterval;
            _actionFetchLimit = actionFetchLimit;

            _pendingLogFlusherTask = StartLogFlusherTask();
            _actionQueueCreatorTask = StartCreateActionQueueTask();

            _jobLogger = loggerFactory?.CreateLogger($"{job.Invocation.Type}({Job.Id})");
        }

        /// <inheritdoc/>
        public void Log(LogLevel logLevel, string message, params object[] logParameters)
        {
            message.ValidateArgument(nameof(message));

            _jobLogger.LogMessage(logLevel, message, logParameters);

            if (logLevel >= _enabledLevel)
            {
                var logEntry = new LogEntry(logLevel, message, logParameters, null);
                lock (_logBuffer)
                {
                    _logBuffer.Add(logEntry);
                }
            }
        }
        /// <inheritdoc/>
        public void Log(LogLevel logLevel, string message, Exception exception, params object[] logParameters)
        {
            message.ValidateArgument(nameof(message));

            _jobLogger.LogMessage(logLevel, message, exception, logParameters);

            if (logLevel >= _enabledLevel)
            {
                var logEntry = new LogEntry(logLevel, message, logParameters, exception);
                lock (_logBuffer)
                {
                    _logBuffer.Add(logEntry);
                }
            }
        }
        /// <inheritdoc/>
        public void Cancel()
        {
            lock (_jobCancellationSource)
            {
                _jobCancellationSource.Cancel();
            }
        }
        private IDelayedPendingTask<IManagedTask> StartLogFlusherTask()
        {
            return _taskManager.ScheduleDelayed(_logFlushInterval, (m, t) =>
            {
                return m.ScheduleActionAsync(this, "LogFlusher", false, async t =>
                {
                    _daemonContext.Log(LogLevel.Information, $"Log flusher task for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> started", Job.Id, Job.Environment);
                    do
                    {
                        // Flush logs
                        _daemonContext.Log(LogLevel.Debug, $"Log flusher task for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> flushing logs", Job.Id, Job.Environment);
                        await FlushLogBuffer(t).ConfigureAwait(false);

                        // Sleep
                        _daemonContext.Log(LogLevel.Debug, $"Log flusher task for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> flushing logs in <{_logFlushInterval}>", Job.Id, Job.Environment);
                        await Helper.Async.Sleep(_logFlushInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    while (!_cancellationTokenSource.Token.IsCancellationRequested);
                    _daemonContext.Log(LogLevel.Information, $"Log flusher task for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> stopped", Job.Id, Job.Environment);
                });
            });
        }
        private IDelayedPendingTask<IManagedTask> StartCreateActionQueueTask()
        {
            return _taskManager.ScheduleDelayed(_actionInterval, (m, t) =>
            {
                return m.ScheduleActionAsync(this, "ActionQueueCreator", false, t =>
                {
                    _daemonContext.Log(LogLevel.Debug, $"Creating action queue to handle actions on executing job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);

                    _pendingActionQueue = new WorkerQueue<ActionInfo>(_taskManager);
                    _ = _pendingActionQueue.OnRequestCreated(PollForActions);
                    _ = _pendingActionQueue.Subscribe(1, ProcessAction);
                });
            });
        }

        private async Task ProcessAction(ActionInfo action, CancellationToken token)
        {
            try
            {
                _daemonContext.Log(LogLevel.Information, $"Received action <{action.Id}> of type <{action.Type}> to execute on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);

                // Validate
                if (!action.ForceExecute && !Job.ExecutionId.Equals(action.ExecutionId))
                {
                    _daemonContext.Log(LogLevel.Warning, $"Execution id of job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> does not match the execution id of action <{action.Id}> of type <{action.Type}>. Action will be skipped");
                    return;
                }

                // Activate
                _daemonContext.Log(LogLevel.Debug, $"Activating action <{action.Id}> of type <{action.Type}> to execute on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                var actionInstance = (await _activator.ActivateAsync(action.Type).ConfigureAwait(false)).CastToOrDefault<TAction>() ?? throw new InvalidOperationException($"Action <{action.Id}> of type <{action.Type}> is not assignable to <{typeof(TAction)}>");

                // Execute
                using (Helper.Time.CaptureDuration(x => _daemonContext.Log(LogLevel.Information, $"Executed action <{action.Id}>({actionInstance}) on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> in <{x.PrintTotalMs()}>", Job.Id, Job.Environment)))
                {
                    _daemonContext.Log(LogLevel.Debug, $"Executing action <{action.Id}>({actionInstance}) on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);

                    await ExecuteActionAsync(actionInstance, action, token).ConfigureAwait(false);
                }

            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _daemonContext.Log(LogLevel.Information, $"Something went wrong while executing action <{action.Id}> of type <{action.Type}> on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
            }
            finally
            {
                // Delete
                _daemonContext.Log(LogLevel.Debug, $"Removing action <{action.Id}>");
                await using (var connection = await _storage.OpenConnectionAsync(true, token))
                {
                    if (await _service.DeleteActionByIdAsync(connection, action.Id, token).ConfigureAwait(false))
                    {
                        await connection.CommitAsync(token).ConfigureAwait(false);
                    }
                    else
                    {
                        _daemonContext.Log(LogLevel.Warning, $"Could not remove action <{action.Id}> of type <{action.Type}> on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    }
                }
            }
        }

        private async Task PollForActions(CancellationToken token)
        {
            _daemonContext.Log(LogLevel.Information, $"Polling for pending actions on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);

            ActionInfo[]? actions = null;

            do
            {
                try
                {
                    // Check for pending items
                    _daemonContext.Log(LogLevel.Debug, $"Checking storage for pending actions on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    await using (var connection = await _storage.OpenConnectionAsync(true, token).ConfigureAwait(false))
                    {
                        actions = await _service.GetNextActionsAsync(connection, Job.Id, _actionFetchLimit, token).ConfigureAwait(false);
                        await connection.CommitAsync(token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while checking for pending actions on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
                }

                // Sleep 
                if (!actions.HasValue())
                {
                    _daemonContext.Log(LogLevel.Debug, $"No pending actions on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Sleeping for <{_actionInterval}>", Job.Id, Job.Environment);

                    await Helper.Async.Sleep(_actionInterval, token).ConfigureAwait(false);
                }
            }
            while (!token.IsCancellationRequested && !actions.HasValue());

            if (actions.HasValue())
            {
                _daemonContext.Log(LogLevel.Information, $"Got <{actions!.Length}> pending actions on job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. Adding to queue", Job.Id, Job.Environment);
                foreach (var action in actions)
                {
                    await _pendingActionQueue.EnqueueAsync(action, token).ConfigureAwait(false);
                }
            }
        }

        private async Task FlushLogBuffer(CancellationToken token)
        {
            await using (await _logLock.LockAsync(token).ConfigureAwait(false))
            {
                LogEntry[]? logEntries = null;
                try
                {
                    lock (_logBuffer)
                    {
                        logEntries = _logBuffer.ToArray();
                        _logBuffer.Clear();
                    }

                    if (logEntries.HasValue())
                    {
                        _daemonContext.Log(LogLevel.Debug, $"Flushing <{logEntries.Length}> logs for job <{HiveLog.Job.IdParam}>", Job.Id);

                        await using (var connection = await _storage.OpenConnectionAsync(true, token).ConfigureAwait(false))
                        {
                            await PersistLogs(connection, Job.Id, logEntries, token).ConfigureAwait(false); 
                            await connection.CommitAsync(token).ConfigureAwait(false);
                        }

                        _daemonContext.Log(LogLevel.Debug, $"Flushed <{logEntries.Length}> logs for job <{HiveLog.Job.IdParam}>", Job.Id);
                    }
                    else
                    {
                        _daemonContext.Log(LogLevel.Debug, $"No logs to flush for job <{HiveLog.Job.IdParam}>", Job.Id);
                    }
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Someting went wrong while trying to persist logs for job <{HiveLog.Job.IdParam}>", ex, Job.Id);
                    lock (_logBuffer)
                    {
                        if (logEntries != null) _logBuffer.IntersectWith(logEntries);
                    }
                }
            }
        }

        /// <summary>
        /// Executes <paramref name="action"/> on the executing job.
        /// </summary>
        /// <param name="action">The activated action as defined in <paramref name="actionInfo"/></param>
        /// <param name="actionInfo">THe info of the action to execute</param>
        /// <param name="token">Token that will be cancelled when the action is requested to stop processing</param>
        protected abstract Task ExecuteActionAsync(TAction action, ActionInfo actionInfo, CancellationToken token);
        /// <summary>
        /// Persists <paramref name="logEntries"/> for job <paramref name="jobId"/> using <paramref name="storageConnection"/>.
        /// </summary>
        /// <param name="storageConnection">The connection/transaction to persist the logs with</param>
        /// <param name="jobId">The job id the logs need to be persisted for</param>
        /// <param name="logEntries">The log entries to persist</param>
        /// <param name="token">Optional token to cancel the request</param>
        protected abstract Task PersistLogs(IStorageConnection storageConnection, string jobId, IEnumerable<LogEntry> logEntries, CancellationToken token);

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            using (new ExecutedAction(x => IsDisposed = x))
            {
                var exceptions = new List<Exception>();
                _daemonContext.Log(LogLevel.Debug, $"Disposing execution context for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);

                // Cancel
                _cancellationTokenSource.Cancel();

                // Cancel tasks
                try
                {
                    _daemonContext.Log(LogLevel.Debug, $"Cancelling tasks for execution context for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    _taskManager.CancelAllFor(this);
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while stopping tasks for execution context for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
                    exceptions.Add(ex);
                }

                // Cancel if not scheduled already
                try
                {
                    _daemonContext.Log(LogLevel.Debug, $"Cancelling action queue creator for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    if (_actionQueueCreatorTask != null) _actionQueueCreatorTask.Cancel();
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while cancelling action queue creator for job <{HiveLog.Job.IdParam}>", ex, Job.Id, Job.Environment);
                }
                try
                {
                    _daemonContext.Log(LogLevel.Debug, $"Cancelling log flusher for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    if (_pendingLogFlusherTask != null) _pendingLogFlusherTask.Cancel();
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while cancelling log flusher for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
                    exceptions.Add(ex);
                }

                // Dispose action queue
                try
                {
                    if (_pendingActionQueue != null)
                    {
                        _daemonContext.Log(LogLevel.Debug, $"Disposing action queue for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                        await _pendingActionQueue.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while disposing action queue for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
                    exceptions.Add(ex);
                }

                // Wait for tasks to stop
                try
                {
                    _daemonContext.Log(LogLevel.Debug, $"Stopping tasks for execution context for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while stopping tasks for execution context for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
                    exceptions.Add(ex);
                }

                // Flush remaining logs
                try
                {
                    _daemonContext.Log(LogLevel.Debug, $"Flushing any remaining logs for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", Job.Id, Job.Environment);
                    await FlushLogBuffer(default).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _daemonContext.Log($"Something went wrong while flushing logs for job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", ex, Job.Id, Job.Environment);
                    exceptions.Add(ex);
                }

                if (exceptions.HasValue()) throw new AggregateException(exceptions);
            }
        }
        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Execution context: {Job}";
        }
    }
}
