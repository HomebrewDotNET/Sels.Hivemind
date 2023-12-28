using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Dispose;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Threading;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sels.HiveMind.Colony.Swarm.Worker
{
    /// <inheritdoc cref="IBackgroundJobExecutionContext"/>
    public class BackgroundJobExecutionContext : IBackgroundJobExecutionContext, IAsyncExposedDisposable
    {
        // Fields
        private readonly IStorage _storage;
        private readonly ITaskManager _taskManager;
        private readonly IDroneState<WorkerSwarmHostOptions> _droneState;
        private readonly ILogger _logger;
        private readonly LogLevel _enabledLevel;
        private readonly TimeSpan _logFlushInterval;
        private readonly IDelayedPendingTask<IManagedTask> _pendingLogFlusherTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _logLock = new SemaphoreSlim(1, 1);
        private readonly HashSet<LogEntry> _logBuffer = new HashSet<LogEntry>();

        // Properties
        /// <inheritdoc/>
        public bool? IsDisposed { get; private set; }
        /// <inheritdoc/>
        public IWriteableBackgroundJob Job { get; }
        /// <inheritdoc/>
        public string Colony { get; }
        /// <inheritdoc/>
        public string Swarm => _droneState.Swarm.Name;
        /// <inheritdoc/>
        public string Drone => _droneState.Name;
        /// <inheritdoc/>
        public object JobInstance { get; }
        /// <inheritdoc/>
        public object[] InvocationArguments { get; }
        /// <inheritdoc/>
        public TimeSpan Duration { get; set; }
        /// <inheritdoc/>
        public object Result { get; set; }

        /// <inheritdoc cref="BackgroundJobExecutionContext"/>
        /// <param name="colony"><see cref="Colony"/></param>
        /// <param name="droneState">The state of the drone executing <paramref name="job"/></param>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="jobInstance"><inheritdoc cref="JobInstance"/></param>
        /// <param name="invocationArguments"><inheritdoc cref="InvocationArguments"/></param>
        /// <param name="enabledLogLevel">The log level above which to persist logs created by the executing background job</param>
        /// <param name="logFlushInterval">How often to flush logs to storage</param>
        /// <param name="storage">The storage to use to persist state</param>
        /// <param name="taskManager">The task manager to use to manage recurring tasks</param>
        /// <param name="logger">Optional logger used to log logs created by the executing background job</param>
        public BackgroundJobExecutionContext(string colony, IDroneState<WorkerSwarmHostOptions> droneState, IWriteableBackgroundJob job, object jobInstance, object[] invocationArguments, LogLevel enabledLogLevel, TimeSpan logFlushInterval, ITaskManager taskManager, IStorage storage, ILogger logger)
        {
            Colony = colony.ValidateArgument(nameof(colony));
            _droneState = droneState.ValidateArgument(nameof(droneState));
            Job = job.ValidateArgument(nameof(job));
            JobInstance = jobInstance;
            InvocationArguments = invocationArguments.ValidateArgument(nameof(invocationArguments));
            _enabledLevel = enabledLogLevel;
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));
            _storage = storage.ValidateArgument(nameof(storage));
            _logFlushInterval = logFlushInterval;
            _logger = logger;

            _pendingLogFlusherTask = StartLogFlusherTask();
        }

        /// <inheritdoc/>
        public void Log(LogLevel logLevel, string message, params object[] logParameters)
        {
            message.ValidateArgument(nameof(message));

            _logger.Log(logLevel, message, logParameters);

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

            _logger.Log(logLevel, exception, message, logParameters);

            if (logLevel >= _enabledLevel)
            {
                var logEntry = new LogEntry(logLevel, message, logParameters, exception);
                lock (_logBuffer)
                {
                    _logBuffer.Add(logEntry);
                }
            }
        }

        private IDelayedPendingTask<IManagedTask> StartLogFlusherTask()
        {
            return _taskManager.ScheduleDelayed(_logFlushInterval, (m, t) =>
            {
                return m.ScheduleActionAsync(this, "LogFlusher", false, async t =>
                {
                    _logger.Log($"Log flusher task for background job <{HiveLog.BackgroundJob.Id}> started");
                    do
                    {
                        // Flush logs
                        _logger.Log($"Log flusher task for background job <{HiveLog.BackgroundJob.Id}> flushing logs", Job.Id);
                        await FlushLogBuffer(t).ConfigureAwait(false);

                        // Sleep
                        _logger.Debug($"Log flusher task for background job <{HiveLog.BackgroundJob.Id}> flushing logs in <{_logFlushInterval}>", Job.Id);
                        await Helper.Async.Sleep(_logFlushInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    while(!_cancellationTokenSource.Token.IsCancellationRequested);
                    _logger.Log($"Log flusher task for background job <{HiveLog.BackgroundJob.Id}> stopped");
                });
            });
        }

        private async Task FlushLogBuffer(CancellationToken token)
        {
            await using (await _logLock.LockAsync(token).ConfigureAwait(false))
            {
                LogEntry[] logEntries = null;
                try
                {
                    lock (_logBuffer)
                    {
                        logEntries = _logBuffer.ToArray();
                        _logBuffer.Clear();
                    }

                    if (logEntries.HasValue())
                    {
                        _logger.Debug($"Flushing <{logEntries.Length}> logs for background job <{HiveLog.BackgroundJob.Id}>", Job.Id);

                        await using (var connection = await _storage.OpenConnectionAsync(true, token).ConfigureAwait(false))
                        {
                            await _storage.CreateBackgroundJobLogsAsync(connection, Job.Id, logEntries, token);
                            await connection.CommitAsync(token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        _logger.Debug($"No logs to flush for background job <{HiveLog.BackgroundJob.Id}>", Job.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Someting went wrong while trying to persist logs for background job <{HiveLog.BackgroundJob.Id}>", ex, Job.Id);
                    lock (_logBuffer)
                    {
                        _logBuffer.IntersectWith(logEntries);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return;

            using (new ExecutedAction(x => IsDisposed = x))
            {
                var exceptions = new List<Exception>();
                _logger.Debug($"Disposing execution context for background job <{HiveLog.BackgroundJob.Id}>", Job.Id);

                // Cancel
                _cancellationTokenSource.Cancel();

                // Cancel if not scheduled already
                try
                {
                    _logger.Debug($"Cancelling log flusher for background job <{HiveLog.BackgroundJob.Id}>", Job.Id);
                    _pendingLogFlusherTask.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.Log($"Something went wrong while cancelling log flusher for background job <{HiveLog.BackgroundJob.Id}>", ex, Job.Id);
                    exceptions.Add(ex);
                }
                
                // Wait for tasks to stop
                try
                {
                    _logger.Debug($"Stopping tasks for execution context for background job <{HiveLog.BackgroundJob.Id}>", Job.Id);
                    await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    _logger.Log($"Something went wrong while stopping tasks for execution context for background job <{HiveLog.BackgroundJob.Id}>", ex, Job.Id);
                    exceptions.Add(ex);
                }

                // Flush remaining logs
                try
                {
                    _logger.Debug($"Flushing any remaining logs for background job <{HiveLog.BackgroundJob.Id}>", Job.Id);
                    await FlushLogBuffer(default).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Something went wrong while flushing logs for background job <{HiveLog.BackgroundJob.Id}>", ex, Job.Id);
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
