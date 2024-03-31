using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Exceptions.Job
{
    /// <summary>
    /// Thrown when a recurring job could not be locked within a configured timeframe for updating.
    /// </summary>
    public class RecurringJobUpdateTimedoutException : Exception
    {
        // Properties
        /// <summary>
        /// The job that could not be locked.
        /// </summary>
        public IReadOnlyRecurringJob Job { get; }
        /// <summary>
        /// The requester that tried to update the job.
        /// </summary>
        public string Requester { get; }

        /// <inheritdoc cref="RecurringJobUpdateTimedoutException"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="requester"><inheritdoc cref="Requester"/></param>
        public RecurringJobUpdateTimedoutException(IReadOnlyRecurringJob job, string requester) : base($"Could not lock recurring job <{job?.Id}> for <{requester}> so it could be updated")
        {
            Job = job.ValidateArgument(nameof(job));
            Requester = requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
        }
    }
}
