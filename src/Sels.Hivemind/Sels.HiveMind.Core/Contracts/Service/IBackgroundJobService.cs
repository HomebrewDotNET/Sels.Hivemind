using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Requests;
using Sels.HiveMind.Client;
using Sels.HiveMind.Query.Job;
using Sels.Core.Extensions;
using System.Linq;
using Sels.HiveMind.Storage.Job.Background;
using Sels.HiveMind.Job.Background;

namespace Sels.HiveMind.Service
{
    /// <summary>
    /// Service used for managing HiveMind background jobs.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobService : IQueryJobService<BackgroundJobStorageData, IBackgroundJobState, JobStateStorageData, QueryBackgroundJobOrderByTarget?>
    {
        /// <summary>
        /// Creates or updates <paramref name="job"/>.
        /// </summary>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="job">The job to save. If <see cref="BackgroundJobStorageData.Id"/> is set to null job will be created, otherwise updated</param>
        /// <param name="releaseLock">If the lock on the job has to be removed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job or <see cref="BackgroundJobStorageData.Id"/> if the job was updated</returns>
        public Task<string> StoreAsync(IStorageConnection connection, BackgroundJobStorageData job, bool releaseLock, CancellationToken token = default);
        /// <summary>
        /// Deletes at most <paramref name="amount"/> background jobs that match the query condition.
        /// </summary>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="amount">How many jobs to delete</param>
        /// <param name="queryConditions">The conditions for which jobs to delete</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The ids of the jobs deleted</returns>
        Task<string[]> DeleteBackgroundJobsAsync(IStorageConnection connection, int amount, JobQueryConditions queryConditions, CancellationToken token = default);
    }
}
