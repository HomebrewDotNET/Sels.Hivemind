using Microsoft.Extensions.Logging;
using Sels.Core;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Represents a log entry created by a running HiveMind component.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// The log level of the log entry.
        /// </summary>
        public LogLevel LogLevel { get; set; }
        /// <summary>
        /// The formatted log message.
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// The type of the exception if one was included in the log entry
        /// </summary>
        public string ExceptionType { get; set; }
        /// <summary>
        /// The exception message if one was included in the log entry.
        /// </summary>
        public string ExceptionMessage { get; set; }
        /// <summary>
        /// The exception stack trace if one was included in the log entry.
        /// </summary>
        public string ExceptionStackTrace { get; set; }
        /// <summary>
        /// The date (in utc) the log entry was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// The date (local machine time) the log entry was created.
        /// </summary>
        public DateTime CreatedAt => CreatedAtUtc.ToLocalTime();

        /// <inheritdoc cref="LogEntry"/>
        public LogEntry()
        {
            
        }

        /// <inheritdoc cref="LogEntry"/>
        /// <param name="logEntry">The instance to copy the properties from</param>
        public LogEntry(LogEntry logEntry)
        {
            logEntry.ValidateArgument(nameof(logEntry));

            LogLevel = logEntry.LogLevel;
            Message = logEntry.Message;
            ExceptionType = logEntry.ExceptionType;
            ExceptionMessage = logEntry.ExceptionMessage;
            ExceptionStackTrace = logEntry.ExceptionStackTrace;
            CreatedAtUtc = logEntry.CreatedAtUtc;
        }

        /// <inheritdoc cref="LogEntry"/>
        public LogEntry(LogLevel logLevel, string message, object[] logParameters, Exception exception)
        {
            LogLevel = logLevel;
            Message = Helper.Strings.FormatAsLog(message, logParameters);
            if (exception != null)
            {
                ExceptionType = exception.GetType().FullName;
                ExceptionMessage = exception.Message;
                ExceptionStackTrace = exception.StackTrace;
            }
            CreatedAtUtc = DateTime.UtcNow;
        }
    }
}
