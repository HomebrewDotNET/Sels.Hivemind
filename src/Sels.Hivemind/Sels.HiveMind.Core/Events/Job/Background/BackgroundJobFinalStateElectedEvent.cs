using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Background
{
    /// <summary>
    /// Raised when a background job was transitioned into a new state that state is being persisted. 
    /// If multiple states are elected before triggering a save only the final state will trigger this event. Does not apply when not using a transaction.
    /// Also triggers on system deletion. 
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
        /// The connection that was used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="BackgroundJobFinalStateElectedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        public BackgroundJobFinalStateElectedEvent(IReadOnlyBackgroundJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            Connection = connection.ValidateArgument(nameof(connection));
        }
    }
}
