﻿using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Requests;
using Sels.HiveMind;

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
        /// <param name="releaseLock">If the lock on the job has to be removed</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job or <see cref="JobStorageData.Id"/> if the job was updated</returns>
        public Task<string> StoreAsync(IStorageConnection connection, JobStorageData job, bool releaseLock, CancellationToken token = default);

        /// <summary>
        /// Tries to acquire an exclusive lock on background job <paramref name="id"/> for <paramref name="requester"/>.
        /// </summary>
        /// <param name="id">The id of the job to lock</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="requester">Who is requesting the lock. When set to null a random value will be used</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The lock state if a lock could be acquired</returns>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        /// <exception cref="BackgroundJobAlreadyLockedException"></exception>
        public Task<LockStorageData> LockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default);
        /// <summary>
        /// Keep the lock on background job <paramref name="id"/> by <paramref name="holder"/> alive by extending the heartbeat.
        /// </summary>
        /// <param name="id">The id of the job to set the heartbeat on</param>
        /// <param name="holder">Who is supposed to hold the lock</param>
        /// <param name="connection">The connection/transaction to execute the action with</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The lock state if the heartbeat was extended</returns>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        /// <exception cref="BackgroundJobAlreadyLockedException"></exception>
        public Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default);
        /// <summary>
        /// Fetches the latest state of background job <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the job to fetch</param>
        /// <param name="connection">The storage connection to use</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <exception cref="BackgroundJobNotFoundException"></exception>
        /// <returns>The latest state of background job <paramref name="id"/></returns>
        public Task<JobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default);

        /// <summary>
        /// Converts <paramref name="job"/> into a format for storage.
        /// </summary>
        /// <param name="job">The job to convert</param>
        /// <returns><paramref name="job"/> converted into a format for storage</returns>
        public Task<JobStorageData> ConvertToStorageFormatAsync(IReadOnlyBackgroundJob job, CancellationToken token = default);

        /// <summary>
        /// Converts state in storage format back into it's original type.
        /// </summary>
        /// <param name="stateData">The state data</param>
        /// <param name="options">The options to convert with</param>
        /// <returns>State instance created from <paramref name="stateData"/></returns>
        public IBackgroundJobState ConvertToState(JobStateStorageData stateData, HiveMindOptions options);
    }
}