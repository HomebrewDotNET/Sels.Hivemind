using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Tracks the changes made on a background job.
    /// </summary>
    public interface IBackgroundJobChangesTracker
    {
        /// <summary>
        /// Indicates if the queue was changed on the job.
        /// </summary>
        bool QueueChanged { get; }
        /// <summary>
        /// Indicates if the priority was changed on the job.
        /// </summary>
        bool PriorityChanged { get; }
        /// <summary>
        /// Indicates if the execution id was regenerated.
        /// </summary>
        bool ExecutionIdChanged { get; }

        /// <summary>
        /// The names of the properties that were added to add job.
        /// </summary>
        public IReadOnlyList<string> NewProperties { get; }
        /// <summary>
        /// The names of the properties that were updated on a job.
        /// </summary>
        public IReadOnlyList<string> UpdatedProperties { get; }
        /// <summary>
        /// The names of the properties that were removed from a job.
        /// </summary>
        public IReadOnlyList<string> RemovedProperties { get; }
        /// <summary>
        /// The states that were added to a background job.
        /// </summary>
        public IReadOnlyList<IBackgroundJobState> NewStates { get; }
    }
}
