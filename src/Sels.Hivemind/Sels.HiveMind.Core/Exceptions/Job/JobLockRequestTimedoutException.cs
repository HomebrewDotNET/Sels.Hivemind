using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Exceptions.Job
{
    /// <summary>
    /// Thrown when a job could not be locked within a configured timeout.
    /// </summary>
    public class JobLockRequestTimedoutException : Exception
    {
        // Properties
        /// <summary>
        /// The job that could not be locked.
        /// </summary>
        public IReadOnlyJob Job { get; }
        /// <summary>
        /// The requester that tried to update the job.
        /// </summary>
        public string Requester { get; }
        /// <summary>
        /// The timeout that was configured.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <inheritdoc cref="JobLockRequestTimedoutException"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="requester"><inheritdoc cref="Requester"/></param>
        public JobLockRequestTimedoutException(IReadOnlyJob job, string requester, TimeSpan timeout) : base($"Could not lock job <{job?.Id}> for <{requester}> within <{timeout}>")
        {
            Job = job.ValidateArgument(nameof(job));
            Requester = requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            Timeout = timeout;
        }
    }
}
