using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Raised when a job is moving to a new state. Elected state can be intercepted.
    /// </summary>
    public class BackgroundJobStateElectionRequest
    {
        // Properties
        /// <summary>
        /// The job the state election was triggered on.
        /// </summary>
        public IWriteableBackgroundJob Job { get; }
        /// <summary>
        /// The old state of the job before election was triggered.
        /// </summary>
        public IBackgroundJobState OldState { get; }
        /// <summary>
        /// The new state the election was triggered for.
        /// </summary>
        public IBackgroundJobState ElectedState => Job.State;

        /// <inheritdoc cref="BackgroundJobStateElectionRequest"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="currentState"><inheritdoc cref="OldState"/></param>
        public BackgroundJobStateElectionRequest(IWriteableBackgroundJob job, IBackgroundJobState currentState)
        {
            Job = job.ValidateArgument(nameof(job));
            OldState = currentState.ValidateArgument(nameof(currentState));
        }
    }
}
