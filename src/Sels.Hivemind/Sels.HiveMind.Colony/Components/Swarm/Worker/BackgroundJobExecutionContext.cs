using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Sels.Core.Dispose;
using Sels.Core.Extensions;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sels.HiveMind.Colony.Swarm.Worker
{
    /// <inheritdoc cref="IBackgroundJobExecutionContext"/>
    public class BackgroundJobExecutionContext : IBackgroundJobExecutionContext, IAsyncExposedDisposable
    {
        // Fields
        private readonly IDroneState<WorkerSwarmHostOptions> _droneState;
        private readonly ILogger _logger;
        private readonly LogLevel _enabledLevel;
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
        /// <param name="logger">Optional logger used to log logs created by the executing background job</param>
        public BackgroundJobExecutionContext(string colony, IDroneState<WorkerSwarmHostOptions> droneState, IWriteableBackgroundJob job, object jobInstance, object[] invocationArguments, LogLevel enabledLogLevel, ILogger logger)
        {
            Colony = colony.ValidateArgument(nameof(colony));
            _droneState = droneState.ValidateArgument(nameof(droneState));
            Job = job.ValidateArgument(nameof(job));
            JobInstance = jobInstance;
            InvocationArguments = invocationArguments.ValidateArgument(nameof(invocationArguments));
            _enabledLevel = enabledLogLevel;
            _logger = logger;
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

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            if (IsDisposed.HasValue) return default;

            using (new ExecutedAction(x => IsDisposed = x))
            {
                return default;
            }          
        }

    }
}
