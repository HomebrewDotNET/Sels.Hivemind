using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Tracks changes made to a job.
    /// </summary>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    public interface IJobChangeTracker<TState>
        where TState : IJobState
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
        public IReadOnlyList<TState> NewStates { get; }
    }
}
