using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a recurring job was transitioned into a new state. 
    /// </summary>
    public class RecurringJobFinalStateElectedEvent
    {
        // Properties
        /// <summary>
        /// The job that was transitioned into a new state.
        /// </summary>
        public IReadOnlyRecurringJob Job { get; }
        /// <summary>
        /// The final state of the job.
        /// </summary>
        public IRecurringJobState FinalState => Job.State;
        /// <summary>
        /// The connection that was used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="RecurringJobFinalStateElectedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        public RecurringJobFinalStateElectedEvent(IReadOnlyRecurringJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            Connection = connection.ValidateArgument(nameof(connection));
        }
    }
}
