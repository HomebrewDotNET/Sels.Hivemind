using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a job was transitioned into a new state. 
    /// </summary>
    public class BackgroundJobFinalStateElectedEvent
    {
        // Properties
        /// <summary>
        /// The job that was transitioned into a new state.
        /// </summary>
        public IReadOnlyBackgroundJob Job { get; }
        /// <summary>
        /// The final state of the job.
        /// </summary>
        public IBackgroundJobState FinalState => Job.State;
        /// <summary>
        /// The storage connection that was used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="BackgroundJobFinalStateElectedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="StorageConnection"/></param>
        public BackgroundJobFinalStateElectedEvent(IReadOnlyBackgroundJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            StorageConnection = connection.ValidateArgument(nameof(connection));
        }
    }
}
