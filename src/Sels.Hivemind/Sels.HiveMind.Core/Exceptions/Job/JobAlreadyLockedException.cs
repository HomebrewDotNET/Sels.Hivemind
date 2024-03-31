using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Thrown when a lock could not be placed on a job.
    /// </summary>
    public class JobAlreadyLockedException : Exception
    {
        // Properties
        /// <summary>
        /// The id of the job that could not be locked.
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// The HiveMind environment of the job.
        /// </summary>
        public string Environment { get; }
        /// <summary>
        /// Who requested the lock.
        /// </summary>
        public string Requester { get; }
        /// <summary>
        /// Who currently holds the lock on the job.
        /// </summary>
        public string Holder { get; }

        /// <inheritdoc cref="JobAlreadyLockedException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        /// <param name="requester"><inheritdoc cref="Requester"/></param>
        /// <param name="holder"><inheritdoc cref="Holder"/></param>
        public JobAlreadyLockedException(string id, string environment, string requester, string holder) : base($"Could not lock job <{id}> in environment <{environment}> for <{requester}> because it is already locked by <{holder}>")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            Requester = requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            Holder = holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
        }
    }
}
