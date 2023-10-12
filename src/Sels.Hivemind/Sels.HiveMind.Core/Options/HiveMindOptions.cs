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
                    .MustBeLargerOrEqualTo(TimeSpan.Zero);
        }
    }
}
