using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <inheritdoc cref="IJobStorageChangeTracker"/>
    public class JobStorageChangeTracker : IJobStorageChangeTracker
    {
        /// <inheritdoc cref="IJobStorageChangeTracker.NewStates"/>
        public List<JobStateStorageData> NewStates { get; } = new List<JobStateStorageData>();
        /// <inheritdoc cref="IJobStorageChangeTracker.NewProperties"/>
        public List<StorageProperty> NewProperties { get; } = new List<StorageProperty>();
        /// <inheritdoc cref="IJobStorageChangeTracker.UpdatedProperties"/>
        public List<StorageProperty> UpdatedProperties { get; } = new List<StorageProperty>();
        /// <inheritdoc cref="IJobStorageChangeTracker.RemovedProperties"/>
        public List<StorageProperty> RemovedProperties { get; } = new List<StorageProperty>();

        /// <inheritdoc/>
        IReadOnlyList<JobStateStorageData> IJobStorageChangeTracker.NewStates => NewStates;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IJobStorageChangeTracker.NewProperties => NewProperties;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IJobStorageChangeTracker.UpdatedProperties => UpdatedProperties;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IJobStorageChangeTracker.RemovedProperties => RemovedProperties;
    }
}
