using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Exposes more information/functionality to executing jobs.
    /// </summary>
    /// <typeparam name="TJob">The type of job being executed</typeparam>
    public interface IJobExecutionContext<TJob>
    {
        // Properties
        /// <summary>
        /// The job being executed.
        /// </summary>
        public TJob Job { get; }
        /// <summary>
        /// The colony executing the job.
        /// </summary>
        public string Colony { get; }
        /// <summary>
        /// The swarm hosted by <see cref="Colony"/> executing the job.
        /// </summary>
        public string Swarm { get; }
        /// <summary>
        /// The drone managed by <see cref="Swarm"/> executing the job.
        /// </summary>
        public string Drone { get; }
        /// <summary>
        /// The instance that will be invoked to execute hte background job.
        /// Can be null if a static method is called.
        /// </summary>
        public object? JobInstance { get; }
        /// <summary>
        /// Array with the current arguments for the target method to call to execute the job.
        /// </summary>
        public object[] InvocationArguments { get; }
        /// <summary>
        /// How long the job took to execute. Does not include middleware itself.
        /// </summary>
        public TimeSpan Duration { get; set; }
        /// <summary>
        /// The result from executing the background job. Can either be the exception if one was thrown or the result returned by the called method. Can also be set/overwritten by middleware.
        /// </summary>
        public object Result { get; set; }

        #region Logging
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
        #endregion

        /// <summary>
        /// Cancels the executing job.
        /// </summary>
        public void Cancel();
    }
}
