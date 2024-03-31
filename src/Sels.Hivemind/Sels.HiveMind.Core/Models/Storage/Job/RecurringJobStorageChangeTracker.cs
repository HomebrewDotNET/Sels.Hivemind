using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <inheritdoc cref="IRecurringJobStorageChangeTracker"/>
    public class RecurringJobStorageChangeTracker : IRecurringJobStorageChangeTracker
    {
        /// <inheritdoc cref="IRecurringJobStorageChangeTracker.NewStates"/>
        public List<RecurringJobStateStorageData> NewStates { get; } = new List<RecurringJobStateStorageData>();
        /// <inheritdoc cref="IRecurringJobStorageChangeTracker.NewProperties"/>
        public List<StorageProperty> NewProperties { get; } = new List<StorageProperty>();
        /// <inheritdoc cref="IRecurringJobStorageChangeTracker.UpdatedProperties"/>
        public List<StorageProperty> UpdatedProperties { get; } = new List<StorageProperty>();
        /// <inheritdoc cref="IRecurringJobStorageChangeTracker.RemovedProperties"/>
        public List<string> RemovedProperties { get; } = new List<string>();

        /// <inheritdoc/>
        IReadOnlyList<RecurringJobStateStorageData> IRecurringJobStorageChangeTracker.NewStates => NewStates;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IRecurringJobStorageChangeTracker.NewProperties => NewProperties;
        /// <inheritdoc/>
        IReadOnlyList<StorageProperty> IRecurringJobStorageChangeTracker.UpdatedProperties => UpdatedProperties;
        /// <inheritdoc/>
        IReadOnlyList<string> IRecurringJobStorageChangeTracker.RemovedProperties => RemovedProperties;
    }
}
