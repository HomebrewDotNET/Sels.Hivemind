using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.DistributedLocking
{
    /// <summary>
    /// Service that can be used to acquire a distributed lock on a resource.
    /// </summary>
    public interface IDistributedLockService
    {
        /// <summary>
        /// Tries to acquire a distributed lock on resource <paramref name="resource"/>.
        /// </summary>
        /// <param name="resource">The resource to try and lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasAcquired: True if the lock was acquired, false if already locked | DistributedLock: The lock if it was acquired</returns>
        Task<(bool WasAcquired, IDistributedLock? DistributedLock)> TryAcquireAsync([Traceable(HiveLog.DistributedLocking.Resource)] string resource, [Traceable(HiveLog.DistributedLocking.Process)] string requester, CancellationToken token = default);
        /// <summary>
        /// Tries to acquire a distributed lock on resource <paramref name="resource"/>. Will wait if it's not free.
        /// </summary>
        /// <param name="resource">The resource to try and lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="timeout">How long to wait before timing out, when set to null wait indefinitely</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The acquired lock</returns>
        Task<IDistributedLock> AcquireAsync([Traceable(HiveLog.DistributedLocking.Resource)] string resource, [Traceable(HiveLog.DistributedLocking.Process)] string requester, TimeSpan? timeout, CancellationToken token = default);

        /// <summary>
        /// Tries to acquire a distributed lock on background job <paramref name="backgroundJobId"/>.
        /// </summary>
        /// <param name="backgroundJobId">The id of the background job to try and lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasAcquired: True if the lock was acquired, false if already locked | DistributedLock: The lock if it was acquired</returns>
        [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
        async Task<(bool WasAcquired, IDistributedLock? DistributedLock)> TryAcquireForBackgroundJobAsync([Traceable(HiveLog.Job.Id)] string backgroundJobId, [Traceable(HiveLog.DistributedLocking.Process)] string requester, CancellationToken token = default)
        {
            backgroundJobId = Guard.IsNotNullOrWhitespace(backgroundJobId);
            requester = Guard.IsNotNullOrWhitespace(requester);

            return await TryAcquireAsync($"BackgroundJob.{backgroundJobId}", requester, token);
        }
        /// <summary>
        /// Tries to acquire a distributed lock on background job <paramref name="backgroundJobId"/>. Will wait if it's not free.
        /// </summary>
        /// <param name="backgroundJobId">The id of the background job to try and lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="timeout">How long to wait before timing out, when set to null wait indefinitely</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The acquired lock</returns>
        [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
        Task<IDistributedLock> AcquireForBackgroundJobAsync([Traceable(HiveLog.Job.Id)] string backgroundJobId, [Traceable(HiveLog.DistributedLocking.Process)] string requester, TimeSpan? timeout, CancellationToken token = default)
        {
            backgroundJobId = Guard.IsNotNullOrWhitespace(backgroundJobId);
            requester = Guard.IsNotNullOrWhitespace(requester);

            return AcquireAsync($"BackgroundJob.{backgroundJobId}", requester, timeout, token);
        }
        /// <summary>
        /// Tries to acquire a distributed lock on recurring job <paramref name="recurringJobId"/>.
        /// </summary>
        /// <param name="recurringJobId">The id of the recurring job to try and lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasAcquired: True if the lock was acquired, false if already locked | DistributedLock: The lock if it was acquired</returns>
        [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
        async Task<(bool WasAcquired, IDistributedLock? DistributedLock)> TryAcquireForRecurringJobAsync([Traceable(HiveLog.Job.Id)] string recurringJobId, [Traceable(HiveLog.DistributedLocking.Process)] string requester, CancellationToken token = default)
        {
            recurringJobId = Guard.IsNotNullOrWhitespace(recurringJobId);
            requester = Guard.IsNotNullOrWhitespace(requester);

            return await TryAcquireAsync($"RecurringJob.{recurringJobId}", requester, token);
        }
        /// <summary>
        /// Tries to acquire a distributed lock on recurring job <paramref name="recurringJobId"/>. Will wait if it's not free.
        /// </summary>
        /// <param name="recurringJobId">The id of the recurring job to try and lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="timeout">How long to wait before timing out, when set to null wait indefinitely</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The acquired lock</returns>
        [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
        Task<IDistributedLock> AcquireForRecurringJobAsync([Traceable(HiveLog.Job.Id)] string recurringJobId, [Traceable(HiveLog.DistributedLocking.Process)] string requester, TimeSpan? timeout, CancellationToken token = default)
        {
            recurringJobId = Guard.IsNotNullOrWhitespace(recurringJobId);
            requester = Guard.IsNotNullOrWhitespace(requester);

            return AcquireAsync($"RecurringJob.{recurringJobId}", requester, timeout, token);
        }
    }
}
