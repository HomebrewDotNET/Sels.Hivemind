using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;
using MySqlConnector;
using Polly.Contrib.WaitAndRetry;

namespace Sels.HiveMind.Queue.MySql
{
    /// <summary>
    /// Exposes more options for a <see cref="HiveMindMySqlQueue"/>.
    /// </summary>
    public class HiveMindMySqlQueueOptions
    {
        /// <summary>
        /// Set to true to automatically deploy the database schema for each configured environment.
        /// </summary>
        public bool DeploySchema { get; set; } = true;
        /// <summary>
        /// The name of the distributed lock that will be used to synchronize deployments.
        /// </summary>
        public string DeploymentLockName { get; set; } = "Deployment";
        /// <summary>
        /// How long to wait for the deployment lock before throwing an error.
        /// </summary>
        public TimeSpan DeploymentLockTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long after the last heartbeat on a dequeued job before it is considered timed out.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// How many timed out dequeued jobs can be unlocked in a single transaction to avoid locking the tables for too long.
        /// </summary>
        public int UnlockBatchSize { get; set; } = 10000;

        /// <summary>
        /// The threshold above which we log a warning if method execution duration goes above it.
        /// </summary>
        public TimeSpan PerformanceWarningThreshold { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// The threshold above which we log an error if method execution duration goes above it.
        /// </summary>
        public TimeSpan PerformanceErrorThreshold { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The maximum amount of times method calls will be retried when a transient <see cref="MySqlException"/> (like deadlocks) is thrown. When set to 0 nothing will be retried.
        /// </summary>
        public int MaxRetryCount { get; set; } = 10;
        /// <summary>
        /// The value that will be used for the medianFirstRetryDelay parameter in <see cref="Backoff.DecorrelatedJitterBackoffV2(TimeSpan, int, int?, bool)"/> when configuring the retry policy.
        /// </summary>
        public TimeSpan MedianFirstRetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="HiveMindMySqlStorageOptions"/>.
    /// </summary>
    public class HiveMindMySqlQueueOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="HiveMindMySqlStorageOptionsValidationProfile"/>
        public HiveMindMySqlQueueOptionsValidationProfile()
        {
            CreateValidationFor<HiveMindMySqlQueueOptions>()
                .ForProperty(x => x.DeploymentLockName)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.LockTimeout)
                    .MustBeLargerOrEqualTo(TimeSpan.FromMinutes(1))
                .ForProperty(x => x.UnlockBatchSize)
                    .MustBeLargerOrEqualTo(1)
                .ForProperty(x => x.PerformanceErrorThreshold)
                    .ValidIf(x => x.Value > x.Source.PerformanceWarningThreshold, x => $"Must be larger than <{nameof(x.Source.PerformanceWarningThreshold)}>")
                .ForProperty(x => x.MaxRetryCount)
                    .MustBeLargerOrEqualTo(0);
        }
    }
}
