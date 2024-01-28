using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Thrown when an acquired lock on a background job has expired.
    /// </summary>
    public class BackgroundJobLockStaleException : Exception
    {
        // Properties
        /// <summary>
        /// The id of the job who's lock has become stale.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The HiveMind environment of the background job.
        /// </summary>
        public string Environment { get; }

        /// <inheritdoc cref="BackgroundJobLockStaleException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        public BackgroundJobLockStaleException(string id, string environment) : base($"Lock on background job <{id}> in environment <{environment}> has become stale")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
