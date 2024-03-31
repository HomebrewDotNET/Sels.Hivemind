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
    /// Raised when a new state is applied (but not necessarily elected) on a recurring job.
    /// </summary>
    public class RecurringJobStateAppliedEvent
    {
        // Properties
        /// <summary>
        /// The job the new state was applied on.
        /// </summary>
        public IWriteableRecurringJob Job { get; }
        /// <summary>
        /// The state that was applied on the job.
        /// </summary>
        public IRecurringJobState AppliedState => Job.State;
        /// <summary>
        /// The state that was unapplied.
        /// </summary>
        public IRecurringJobState UnappliedState => Job.StateHistory.Last();
        /// <summary>
        /// Optional storage connection the state was changed with. Can be null.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="RecurringJobStateAppliedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="StorageConnection"/></param>
        public RecurringJobStateAppliedEvent(IWriteableRecurringJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            StorageConnection = connection;
        }
    }
}
