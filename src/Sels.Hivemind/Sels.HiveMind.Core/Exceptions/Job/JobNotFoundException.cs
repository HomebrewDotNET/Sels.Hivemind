using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Thrown when a job could not be found.
    /// </summary>
    public class JobNotFoundException : Exception
    {
        // Properties
        /// <summary>
        /// The id of the job that could not be found.
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// The HiveMind environment where the job could not be found in.
        /// </summary>
        public string Environment { get; }

        /// <inheritdoc cref="JobNotFoundException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        public JobNotFoundException(string id, string environment) : base($"Could not find job <{id}> in environment <{environment}>")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
