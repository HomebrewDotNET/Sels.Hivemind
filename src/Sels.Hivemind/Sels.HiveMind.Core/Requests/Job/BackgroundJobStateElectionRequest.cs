using Sels.Core.Extensions;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Raised when a job is moving to a new state. Elected state can be intercepted.
    /// </summary>
    public class BackgroundJobStateElectionRequest : IRequest<IBackgroundJobState>
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
        /// <summary>
        /// Optional storage connection the request is being performed with. Can be null.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="BackgroundJobStateElectionRequest"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="currentState"><inheritdoc cref="OldState"/></param>
        /// <param name="storageConnection"><inheritdoc cref="StorageConnection"/></param>
        public BackgroundJobStateElectionRequest(IWriteableBackgroundJob job, IBackgroundJobState currentState, IStorageConnection storageConnection)
        {
            Job = job.ValidateArgument(nameof(job));
            OldState = currentState.ValidateArgument(nameof(currentState));
            StorageConnection = storageConnection;
        }
    }
}
