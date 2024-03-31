using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <inheritdoc cref="IBackgroundJobStorageChangeTracker"/>
    public class BackgroundJobStorageChangeTracker : IBackgroundJobStorageChangeTracker
    {
        /// <inheritdoc cref="IBackgroundJobStorageChangeTracker.NewStates"/>
        public List<JobStateStorageData> NewStates { get; } = new List<JobStateStorageData>();
        /// <inheritdoc cref="IBackgroundJobStorageChangeTracker.NewProperties"/>
        public List<StorageProperty> NewProperties { get; } = new List<StorageProperty>();
        /// <inheritdoc cref="IBackgroundJobStorageChangeTracker.UpdatedProperties"/>
        public List<StorageProperty> UpdatedProperties { get; } = new List<StorageProperty>();
        /// <inheritdoc cref="IBackgroundJobStorageChangeTracker.RemovedProperties"/>
        public List<string> RemovedProperties { get; } = new List<string>();

        /// <inheritdoc/>
        IReadOnlyList<JobStateStorageData> IBackgroundJobStorageChangeTracker.NewStates => NewStates;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IBackgroundJobStorageChangeTracker.NewProperties => NewProperties;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IBackgroundJobStorageChangeTracker.UpdatedProperties => UpdatedProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IBackgroundJobStorageChangeTracker.RemovedProperties => RemovedProperties;
    }
}
