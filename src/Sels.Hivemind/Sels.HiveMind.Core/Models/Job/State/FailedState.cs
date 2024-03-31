using Sels.Core.Extensions;
using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Job failed to execute.
    /// </summary>
    public class FailedState : BaseSharedJobState<FailedState>
    {
        // Properties
        /// <summary>
        /// The type of the exception that caused the failure.
        /// </summary>
        public Type ExceptionType { get; private set; }
        /// <summary>
        /// The error message.
        /// </summary>
        public string Message { get; private set; }
        /// <summary>
        /// The stack trace of the exception.
        /// </summary>
        public string StackTrace { get; private set; }
        /// <summary>
        /// The exception that caused the failure.
        /// Object is only available during events before the job is persisted.
        /// </summary>
        [IgnoredStateProperty]
        public Exception Exception { get; private set; }

        /// <inheritdoc cref="FailedState"/>
        /// <param name="exception"><inheritdoc cref="Exception"/></param>
        public FailedState(Exception exception)
        {
            Exception = exception.ValidateArgument(nameof(exception));
            Message = exception.Message;
            ExceptionType = exception.GetType();
            StackTrace = exception.StackTrace;
        }

        /// <inheritdoc cref="FailedState"/>
        /// <param name="message"><inheritdoc cref="Message"/></param>
        /// <param name="exceptionType"><inheritdoc cref="ExceptionType"/></param>
        /// <param name="stackTrace"><inheritdoc cref="StackTrace"/></param>
        public FailedState(string message = null, Type exceptionType = null, string stackTrace = null)
        {
            Message = message;
            ExceptionType = exceptionType;
            StackTrace = stackTrace;
        }
    }
}
