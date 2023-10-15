using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Thrown when a background job could not be found.
    /// </summary>
    public class BackgroundJobNotFoundException : Exception
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

        /// <inheritdoc cref="BackgroundJobNotFoundException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        public BackgroundJobNotFoundException(string id, string environment) : base($"Could not find background job <{id}> in environment <{environment}>")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
