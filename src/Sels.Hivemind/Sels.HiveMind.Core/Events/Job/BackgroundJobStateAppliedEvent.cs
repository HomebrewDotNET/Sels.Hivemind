using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a new state is applied (but not necessarily elected) on a job.
    /// </summary>
    public class BackgroundJobStateAppliedEvent
    {
        // Properties
        /// <summary>
        /// The job the new state was applied on.
        /// </summary>
        public IWriteableBackgroundJob Job { get; }
        /// <summary>
        /// The state that was applied on the job.
        /// </summary>
        public IBackgroundJobState AppliedState => Job.State;
        /// <summary>
        /// The state that was unapplied.
        /// </summary>
        public IBackgroundJobState UnappliedState => Job.StateHistory.Last();
        /// <summary>
        /// Optional storage connection the state was changed with. Can be null.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="BackgroundJobStateAppliedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="StorageConnection"/></param>
        public BackgroundJobStateAppliedEvent(IWriteableBackgroundJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            StorageConnection = connection;
        }
    }
}
