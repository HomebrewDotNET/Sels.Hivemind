using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Thrown when an acquired lock on a job has expired.
    /// </summary>
    public class JobLockStaleException : Exception
    {
        // Properties
        /// <summary>
        /// The id of the job who's lock has become stale.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The HiveMind environment of the job.
        /// </summary>
        public string Environment { get; }

        /// <inheritdoc cref="JobLockStaleException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        public JobLockStaleException(string id, string environment) : base($"Lock on job <{id}> in environment <{environment}> has become stale")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
