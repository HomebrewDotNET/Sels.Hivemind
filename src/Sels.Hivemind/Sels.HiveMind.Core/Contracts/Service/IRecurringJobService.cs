using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Service
{
    /// <summary>
    /// Service used for managing HiveMind recurring jobs.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IRecurringJobService : IQueryJobService<RecurringJobStorageData, IRecurringJobState, JobStateStorageData, QueryRecurringJobOrderByTarget?>
    {
        /// <summary>
        /// Tries to create recurring job using the configuration in <paramref name="storageData"/> if it does not exist yet.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="storageData">The state used to create the new recurring job</param>
        /// <param name="token">Optional token that can be used to cancel the request</param>
        /// <returns>The current state of the recurring job</returns>
        Task<RecurringJobStorageData> TryCreateAsync(IStorageConnection connection, RecurringJobConfigurationStorageData storageData, CancellationToken token = default);
        /// <summary>
        /// Tries the update recurring job if it is still locked.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="recurringJob">The new state to update to</param>
        /// <param name="releaseLock">If the lock on the job has to be removed</param>
        /// <param name="token">Optional token that can be used to cancel the request</param>
        Task TryUpdateAsync(IStorageConnection connection, RecurringJobStorageData recurringJob, bool releaseLock, CancellationToken token = default);
    }
}
