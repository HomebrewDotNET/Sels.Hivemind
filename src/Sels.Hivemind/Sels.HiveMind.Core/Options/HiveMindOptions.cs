using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains extra configuration for hive mind components.
    /// </summary>
    public class HiveMindOptions
    {
        /// <summary>
        /// The prefix that will used for all cache keys.
        /// </summary>
        public string CachePrefix { get; set; } = "Sels.HiveMind";
        /// <summary>
        /// How long delegates for background job state stay cached. The delegates are used when serializing and deserializing states from storage.
        /// </summary>
        public TimeSpan BackgroundJobStateDelegateExpiryTime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long after the last heartbeat on a lock before it's considered timed out.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// How much time before a heartbeat on a lock expires to set it again.
        /// </summary>
        public TimeSpan LockHeartbeatOffset => LockTimeout / 2;
        /// <summary>
        /// If the remaining time before a lock can time out is below the offset, the heartbeat will be set to ensure actions are executed with a valid lock. 
        /// (e.g. 5 seconds before a lock can time out a save is requested on a job, with the offset set to 10 seconds the heartbeat will be set before performing the save)
        /// </summary>
        public TimeSpan LockExpirySafetyOffset { get; set; } = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Contains validation rules for <see cref="HiveMindOptions"/>.
    /// </summary>
    public class HiveMindOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="HiveMindOptionsValidationProfile"/>
        public HiveMindOptionsValidationProfile()
        {
            CreateValidationFor<HiveMindOptions>()
                .ForProperty(x => x.CachePrefix)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.BackgroundJobStateDelegateExpiryTime)
                    .MustBeLargerOrEqualTo(TimeSpan.Zero)
                .ForProperty(x => x.LockTimeout)
                    .MustBeLargerOrEqualTo(TimeSpan.FromMinutes(1))
                .ForProperty(x => x.LockExpirySafetyOffset)
                    .ValidIf(x => x.Value < x.Source.LockTimeout, x => $"Must be smaller than {nameof(x.Source.LockTimeout)}");
        }
    }
}
