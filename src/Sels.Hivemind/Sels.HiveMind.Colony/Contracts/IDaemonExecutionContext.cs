using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Execution context for tasks executed by a <see cref="Daemon"/>.
    /// </summary>
    public interface IDaemonExecutionContext
    {
        /// <summary>
        /// The daemon currently executing the task.
        /// </summary>
        public IWriteableDaemon Daemon { get; }
        /// <summary>
        /// Service provider that can be used by the task to resolve dependencies.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }
        /// <summary>
        /// Delegate that will be called by the daemon to get the latest processing state.
        /// </summary>
        public Func<object?>? StateGetter { get; set; }

        /// <summary>
        /// Adds a new log entry.
        /// </summary>
        /// <param name="logLevel">The log level for the log entry</param>
        /// <param name="message">The log message. Can be formatted</param>
        /// <param name="logParameters">Optional log parameters for <paramref name="message"/></param>
        public void Log(LogLevel logLevel, string message, params object[] logParameters);
        /// <summary>
        /// Adds a new log entry with log level <see cref="LogLevel.Information"/>.
        /// </summary>
        /// <param name="message">The log message. Can be formatted</param>
        /// <param name="logParameters">Optional log parameters for <paramref name="message"/></param>
        public void Log(string message, params object[] logParameters) => Log(LogLevel.Information, message, logParameters);
        /// <summary>
        /// Adds a new log entry.
        /// </summary>
        /// <param name="logLevel">The log level for the log entry</param>
        /// <param name="message">The log message. Can be formatted</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="logParameters">Optional log parameters for <paramref name="message"/></param>
        public void Log(LogLevel logLevel, string message, Exception exception, params object[] logParameters);
        /// <summary>
        /// Adds a new log entry with log level <see cref="LogLevel.Error"/>.
        /// </summary>
        /// <param name="message">The log message. Can be formatted</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="logParameters">Optional log parameters for <paramref name="message"/></param>
        public void Log(string message, Exception exception, params object[] logParameters) => Log(LogLevel.Error, message, exception, logParameters);  
    }
}
