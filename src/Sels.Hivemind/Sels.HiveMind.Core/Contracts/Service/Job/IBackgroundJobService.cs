using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;

namespace Sels.HiveMind.Service.Job
{
    /// <summary>
    /// Service used for managing HiveMind background jobs.
    /// </summary>
    public interface IBackgroundJobService
    {
        /// <summary>
        /// Creates or updates <paramref name="job"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="job">The job to save. If <see cref="JobStorageData.Id"/> is set to null job will be created, otherwise updated</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job or <see cref="JobStorageData.Id"/> if the job was updated</returns>
        public Task<string> StoreAsync(IStorageConnection connection, JobStorageData job, CancellationToken token = default);

        /// <summary>
        /// Converts <paramref name="job"/> into a format for storage.
        /// </summary>
        /// <param name="job">The job to convert</param>
        /// <returns><paramref name="job"/> converted into a format for storage</returns>
        public Task<JobStorageData> ConvertToStorageFormatAsync(IReadOnlyBackgroundJob job, CancellationToken token = default);
    }
}
