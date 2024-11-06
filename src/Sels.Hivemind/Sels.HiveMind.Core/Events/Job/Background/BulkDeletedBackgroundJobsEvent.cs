using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Events.Job.Background
{
    /// <summary>
    /// Events that is raised when a batch of background jobs was deleted in bulk.
    /// Only triggered by the deletion daemon.
    /// Transaction can still be active.
    /// </summary>
    public class BulkDeletedBackgroundJobsEvent
    {
        /// <summary>
        /// The jobs that were deleted.
        /// </summary>
        public string[] BackgroundJobIds { get; }
        /// <summary>
        /// The storage connection that was used to delete the jobs.
        /// Transaction can still be active.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="BulkDeletedBackgroundJobsEvent"/>
        /// <param name="jobIds"><inheritdoc cref="BackgroundJobIds"/></param>
        /// <param name="storageConnection"><inheritdoc cref="StorageConnection"/></param>
        public BulkDeletedBackgroundJobsEvent(IEnumerable<string> jobIds, IStorageConnection storageConnection)
        {
            BackgroundJobIds = jobIds.ValidateArgument(nameof(jobIds)).ToArray();
            StorageConnection = storageConnection.ValidateArgument(nameof(storageConnection));
        }
    }
}
