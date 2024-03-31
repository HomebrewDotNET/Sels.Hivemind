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
    /// Raised when a recurring job is moving to a new state. Elected state can be intercepted.
    /// </summary>
    public class RecurringJobStateElectionRequest : IRequest<IRecurringJobState>
    {
        // Properties
        /// <summary>
        /// The job the state election was triggered on.
        /// </summary>
        public IWriteableRecurringJob Job { get; }
        /// <summary>
        /// The old state of the job before election was triggered.
        /// </summary>
        public IRecurringJobState OldState { get; }
        /// <summary>
        /// The new state the election was triggered for.
        /// </summary>
        public IRecurringJobState ElectedState => Job.State;
        /// <summary>
        /// Optional storage connection the request is being performed with. Can be null.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="RecurringJobStateElectionRequest"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="currentState"><inheritdoc cref="OldState"/></param>
        /// <param name="storageConnection"><inheritdoc cref="StorageConnection"/></param>
        public RecurringJobStateElectionRequest(IWriteableRecurringJob job, IRecurringJobState currentState, IStorageConnection storageConnection)
        {
            Job = job.ValidateArgument(nameof(job));
            OldState = currentState.ValidateArgument(nameof(currentState));
            StorageConnection = storageConnection;
        }
    }
}
