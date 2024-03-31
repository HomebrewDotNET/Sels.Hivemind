using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Used to keep track of changes on a job
    /// </summary>
    /// <typeparam name="TState">The type of state used by the job</typeparam>
    public interface IJobChangeLog<TState> : IJobChangeTracker<TState>
        where TState : IJobState
    {
        /// <inheritdoc/>
        public new bool QueueChanged { get; set; }
        /// <inheritdoc/>
        public new bool PriorityChanged { get; set; }

        /// <inheritdoc cref="IRecurringJobChangeTracker.NewProperties"/>
        public new List<string> NewProperties { get; }
        /// <inheritdoc cref="IRecurringJobChangeTracker.UpdatedProperties"/>
        public new List<string> UpdatedProperties { get; } 
        /// <inheritdoc cref="IRecurringJobChangeTracker.RemovedProperties"/>
        public new List<string> RemovedProperties { get; }
        /// <inheritdoc cref="IRecurringJobChangeTracker.NewStates"/>
        public new List<TState> NewStates { get; }
    }
}
