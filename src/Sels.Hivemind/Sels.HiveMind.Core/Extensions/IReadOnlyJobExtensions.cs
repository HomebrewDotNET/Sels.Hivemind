using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Exceptions.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Contains extensions for <see cref="IReadOnlyJob{TLockedJob, TChangeTracker, TState, TAction}"/>
    /// </summary>
    public static class IReadOnlyJobExtensions
    {
        /// <summary>
        /// Tries to lock <paramref name="job"/> for <paramref name="requester"/>. Will wait for the job to be unlocked if it is already locked.
        /// </summary>
        /// <typeparam name="TLockedJob">The type of the locked job</typeparam>
        /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
        /// <typeparam name="TState">The type of state used by the job</typeparam>
        /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
        /// <param name="job">The job to lock</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="pollingInterval">How often to try and lock the job</param>
        /// <param name="timeout">The maximum amount of time to wait for the job to be locked. When set to null no timeout will be used</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <param name="cancellationToken">Token that can be used to cnacel the request</param>
        /// <returns><paramref name="job"/> with an active lock</returns>
        public static async Task<TLockedJob> WaitForLockAsync<TLockedJob, TChangeTracker, TState, TAction>(this IReadOnlyJob<TLockedJob, TChangeTracker, TState, TAction> job, string requester, TimeSpan? pollingInterval = default, TimeSpan? timeout = null, ILogger logger = null, CancellationToken cancellationToken = default)
            where TState : IJobState
            where TChangeTracker : IJobChangeTracker<TState>
        {
            job.ValidateArgument(nameof(job));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            // Wait for lock
            using (var timeoutTokenSource = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan))
            {
                using var tokenRegistration = cancellationToken.Register(timeoutTokenSource.Cancel);

                while (timeoutTokenSource.Token.IsCancellationRequested)
                {
                    // Sleep
                    logger.Debug($"Trying to lock job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> again in <{pollingInterval}>", job.Id, job.Environment);
                    await Helper.Async.Sleep(pollingInterval.Value, timeoutTokenSource.Token).ConfigureAwait(false);
                    if (timeoutTokenSource.Token.IsCancellationRequested) throw new JobLockRequestTimedoutException(job, requester, timeout.Value);

                    if (await job.TryLockAsync(requester, cancellationToken).ConfigureAwait(false) is (true, var lockedJob))
                    {
                        logger.Debug($"Acquired lock on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, job.Environment);
                        return lockedJob;
                    }
                    else
                    {
                        logger.Debug($"Could not lock job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", job.Id, job.Environment);
                    }
                }

                throw new JobLockRequestTimedoutException(job, requester, timeout.Value);
            }
        }

        /// <summary>
        /// Tries to lock <paramref name="job"/> for <paramref name="requester"/>. Will wait for the job to be unlocked if it is already locked.
        /// </summary>
        /// <typeparam name="TLockedJob">The type of the locked job</typeparam>
        /// <typeparam name="TChangeTracker">The type of change tracker used</typeparam>
        /// <typeparam name="TState">The type of state used by the job</typeparam>
        /// <typeparam name="TAction">The type of action that can be scheduled on the job if it's running</typeparam>
        /// <param name="job">The job to lock</param>
        /// <param name="storageConnection">The connection to use to lock the job</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="pollingInterval">How often to try and lock the job</param>
        /// <param name="timeout">The maximum amount of time to wait for the job to be locked. When set to null no timeout will be used</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <param name="cancellationToken">Token that can be used to cnacel the request</param>
        /// <returns><paramref name="job"/> with an active lock</returns>
        public static async Task<TLockedJob> WaitForLockAsync<TLockedJob, TChangeTracker, TState, TAction>(this IReadOnlyJob<TLockedJob, TChangeTracker, TState, TAction> job, IStorageConnection storageConnection, string requester, TimeSpan pollingInterval, TimeSpan? timeout = null, ILogger logger = null, CancellationToken cancellationToken = default)
            where TState : IJobState
            where TChangeTracker : IJobChangeTracker<TState>
        {
            job.ValidateArgument(nameof(job));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            storageConnection.ValidateArgument(nameof(storageConnection));

            if(await job.TryLockAsync(storageConnection, requester, cancellationToken).ConfigureAwait(false) is (true, var initialLockedJob))
            {
                logger.Debug($"Acquired lock on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, job.Environment);
                return initialLockedJob;
            }

            // Wait for lock
            using (var timeoutTokenSource = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan))
            {
                using var tokenRegistration = cancellationToken.Register(timeoutTokenSource.Cancel);

                while (!timeoutTokenSource.Token.IsCancellationRequested)
                {
                    // Sleep
                    logger.Debug($"Trying to lock job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> again in <{pollingInterval}>", job.Id, job.Environment);
                    await Helper.Async.Sleep(pollingInterval, timeoutTokenSource.Token).ConfigureAwait(false);
                    if (timeoutTokenSource.Token.IsCancellationRequested) throw new JobLockRequestTimedoutException(job, requester, timeout.Value);

                    if (await job.TryLockAsync(storageConnection, requester, cancellationToken).ConfigureAwait(false) is (true, var lockedJob))
                    {
                        logger.Debug($"Acquired lock on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, job.Environment);
                        return lockedJob;
                    }
                    else
                    {
                        logger.Debug($"Could not lock job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", job.Id, job.Environment);
                    }
                }

                throw new JobLockRequestTimedoutException(job, requester, timeout.Value);
            }
        }
    }
}
