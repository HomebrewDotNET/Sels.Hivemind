using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Storage;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Events that is raised when a batch of background jobs are moved to the <see cref="SystemDeletedState"/>.
    /// Transaction can still be active.
    /// </summary>
    public class SystemDeletedBackgroundJobsEvent
    {
        /// <summary>
        /// The jobs that were deleted.
        /// </summary>
        public IReadOnlyBackgroundJob[] BackgroundJobs { get; }
        /// <summary>
        /// The storage connection that was used to delete the jobs.
        /// Transaction can still be active.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="SystemDeletedState"/>
        /// <param name="jobs"><inheritdoc cref="BackgroundJobs"/></param>
        /// <param name="storageConnection"><inheritdoc cref="StorageConnection"/></param>
        public SystemDeletedBackgroundJobsEvent(IEnumerable<IReadOnlyBackgroundJob> jobs, IStorageConnection storageConnection)
        {
            BackgroundJobs = jobs.ValidateArgument(nameof(jobs)).ToArray();
            StorageConnection = storageConnection.ValidateArgument(nameof(storageConnection));
        }
    }
}
