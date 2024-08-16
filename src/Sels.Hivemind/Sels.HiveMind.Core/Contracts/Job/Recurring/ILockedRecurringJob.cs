using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.Core.Extensions;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Represents a locked recurring job where the state can be modified and saved or where the job can be deleted. Disposing the job will release the lock if one is still set.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface ILockedRecurringJob : ILockedJob<ILockedRecurringJob, IRecurringJobChangeTracker, IRecurringJobState, IRecurringJobAction>, IWriteableRecurringJob
    {
        /// <summary>
        /// Saves any changes made to the recurring job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task UpdateAsync(IStorageConnection connection, bool retainLock, CancellationToken token = default);
        /// <summary>
        /// Saves any changes made to the recurring job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task UpdateAsync(IClientConnection connection, bool retainLock, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));

            return UpdateAsync(connection.StorageConnection, retainLock, token);
        }
        /// <summary>
        /// Saves any changes made to the recurring job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task UpdateAsync(IClientConnection connection, CancellationToken token = default) => UpdateAsync(connection, false, token);
        /// <summary>
        /// Saves any changes made to the recurring job.
        /// </summary>
        /// <param name="connection">The connection/transaction to execute the save with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task UpdateAsync(IStorageConnection connection, CancellationToken token = default) => UpdateAsync(connection, false, token);
        /// <summary>
        /// Saves any changes made to the recurring job.
        /// </summary>
        /// <param name="retainLock">True to retain the lock on the background job after saving, otherwise false to release the lock after saving</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task UpdateAsync(bool retainLock, CancellationToken token = default);
        /// <summary>
        /// Saves any changes made to the recurring job. Lock will be released after saving.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        public Task UpdateAsync(CancellationToken token = default) => UpdateAsync(false, token);
    }
}
