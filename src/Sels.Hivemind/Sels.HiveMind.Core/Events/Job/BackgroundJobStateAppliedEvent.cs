using Sels.Core.Extensions;
using Sels.HiveMind.Job;
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

        /// <inheritdoc cref="BackgroundJobStateAppliedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        public BackgroundJobStateAppliedEvent(IWriteableBackgroundJob job)
        {
            Job = job.ValidateArgument(nameof(job));
        }
    }
}
