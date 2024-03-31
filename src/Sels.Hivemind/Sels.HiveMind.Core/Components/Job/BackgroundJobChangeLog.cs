using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Tracks the changes made to a background job.
    /// </summary>
    public class BackgroundJobChangeLog : IBackgroundJobChangeTracker, IJobChangeLog<IBackgroundJobState>
    {
        /// <inheritdoc/>
        public bool QueueChanged { get; set; }
        /// <inheritdoc/>
        public bool PriorityChanged { get; set; }

        /// <inheritdoc cref="IBackgroundJobChangeTracker.NewProperties"/>
        public List<string> NewProperties { get; } = new List<string>();
        /// <inheritdoc cref="IBackgroundJobChangeTracker.UpdatedProperties"/>
        public List<string> UpdatedProperties { get; } = new List<string>();
        /// <inheritdoc cref="IBackgroundJobChangeTracker.RemovedProperties"/>
        public List<string> RemovedProperties { get; } = new List<string>();
        /// <inheritdoc cref="IBackgroundJobChangeTracker.NewStates"/>
        public List<IBackgroundJobState> NewStates { get; } = new List<IBackgroundJobState>();

        /// <inheritdoc/>
        IReadOnlyList<string> IJobChangeTracker<IBackgroundJobState>.NewProperties => NewProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IJobChangeTracker<IBackgroundJobState>.UpdatedProperties => UpdatedProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IJobChangeTracker<IBackgroundJobState>.RemovedProperties => RemovedProperties;
        /// <inheritdoc/>
        IReadOnlyList<IBackgroundJobState> IJobChangeTracker<IBackgroundJobState>.NewStates => NewStates;
    }
}
