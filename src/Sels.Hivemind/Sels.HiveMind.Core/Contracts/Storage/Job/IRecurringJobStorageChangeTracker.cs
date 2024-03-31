using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <summary>
    /// Contains the changes made to a <see cref="RecurringJobStorageData"/>.
    /// </summary>
    public interface IRecurringJobStorageChangeTracker
    {
        /// <summary>
        /// The states that were added on a job.
        /// </summary>
        public IReadOnlyList<RecurringJobStateStorageData> NewStates { get; }
        /// <summary>
        /// Properties that were added to a job.
        /// </summary>
        public IReadOnlyList<StorageProperty> NewProperties { get; }
        /// <summary>
        /// Properties that were updated on a job.
        /// </summary>
        public IReadOnlyList<StorageProperty> UpdatedProperties { get; }
        /// <summary>
        /// Names of the properties that were removed from a job.
        /// </summary>
        public IReadOnlyList<string> RemovedProperties { get; }
    }
}
