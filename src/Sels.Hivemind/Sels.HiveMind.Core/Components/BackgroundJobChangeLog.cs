using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Tracks the changes made to a background job.
    /// </summary>
    public class BackgroundJobChangeLog : IBackgroundJobChangesTracker
    {
        /// <inheritdoc/>
        public bool QueueChanged { get; set; }
        /// <inheritdoc/>
        public bool PriorityChanged { get; set; }
        /// <inheritdoc/>
        public bool ExecutionIdChanged { get; set; }

        /// <inheritdoc cref="IBackgroundJobChangesTracker.NewProperties"/>
        public List<string> NewProperties { get; } = new List<string>();
        /// <inheritdoc cref="IBackgroundJobChangesTracker.UpdatedProperties"/>
        public List<string> UpdatedProperties { get; } = new List<string>();
        /// <inheritdoc cref="IBackgroundJobChangesTracker.RemovedProperties"/>
        public List<string> RemovedProperties { get; } = new List<string>();
        /// <inheritdoc cref="IBackgroundJobChangesTracker.NewStates"/>
        public List<IBackgroundJobState> NewStates { get; } = new List<IBackgroundJobState>();

        /// <inheritdoc/>
        IReadOnlyList<string> IBackgroundJobChangesTracker.NewProperties => NewProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IBackgroundJobChangesTracker.UpdatedProperties => UpdatedProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IBackgroundJobChangesTracker.RemovedProperties => RemovedProperties;
        /// <inheritdoc/>
        IReadOnlyList<IBackgroundJobState> IBackgroundJobChangesTracker.NewStates => NewStates;
    }
}
