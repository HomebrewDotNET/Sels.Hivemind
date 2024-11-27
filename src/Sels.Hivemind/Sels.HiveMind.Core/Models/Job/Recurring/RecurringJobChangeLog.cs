using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Tracks the changes made to a recurring job.
    /// </summary>
    public class RecurringJobChangeLog : IRecurringJobChangeTracker, IJobChangeLog<IRecurringJobState>
    {
        /// <inheritdoc/>
        public bool QueueChanged { get; set; }
        /// <inheritdoc/>
        public bool PriorityChanged { get; set; }

        /// <inheritdoc cref="IRecurringJobChangeTracker.NewProperties"/>
        public List<string> NewProperties { get; } = new List<string>();
        /// <inheritdoc cref="IRecurringJobChangeTracker.UpdatedProperties"/>
        public List<string> UpdatedProperties { get; } = new List<string>();
        /// <inheritdoc cref="IRecurringJobChangeTracker.RemovedProperties"/>
        public List<string> RemovedProperties { get; } = new List<string>();
        /// <inheritdoc cref="IRecurringJobChangeTracker.NewStates"/>
        public List<IRecurringJobState> NewStates { get; } = new List<IRecurringJobState>();

        /// <inheritdoc/>
        IReadOnlyList<string> IJobChangeTracker<IRecurringJobState>.NewProperties => NewProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IJobChangeTracker<IRecurringJobState>.UpdatedProperties => UpdatedProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IJobChangeTracker<IRecurringJobState>.RemovedProperties => RemovedProperties;
        /// <inheritdoc/>
        IReadOnlyList<IRecurringJobState> IJobChangeTracker<IRecurringJobState>.NewStates => NewStates;
    }
}
