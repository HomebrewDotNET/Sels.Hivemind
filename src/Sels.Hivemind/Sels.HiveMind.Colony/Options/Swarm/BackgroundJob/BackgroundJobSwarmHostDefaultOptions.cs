using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob
{
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="BackgroundJobSwarmHostOptions{TOptions}"/>.
    /// </summary>
    public class BackgroundJobSwarmHostDefaultOptions : SwarmHostDefaultOptions
    {
        /// <summary>
        /// The default value for <see cref="BackgroundJobSwarmHostOptions{TOptions}.MaxJobCommitTime"/>.
        /// </summary>
        public TimeSpan MaxJobCommitTime { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// The default value for <see cref="BackgroundJobSwarmHostOptions{TOptions}.MaxNotFoundWaitTime"/>.
        /// </summary>
        public TimeSpan MaxNotFoundWaitTime { get; set; } = TimeSpan.FromMilliseconds(250);
        /// <summary>
        /// The default value for <see cref="BackgroundJobSwarmHostOptions{TOptions}.NotFoundCheckInterval"/>.
        /// </summary>
        public int NotFoundCheckInterval { get; set; } = 2;
        /// <summary>
        /// The default value for <see cref="BackgroundJobSwarmHostOptions{TOptions}.LockedDelay"/>.
        /// </summary>
        public TimeSpan LockedDelay { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// The default value for <see cref="BackgroundJobSwarmHostOptions{TOptions}.LockHeartbeatSafetyOffset"/>.
        /// </summary>
        public TimeSpan LockHeartbeatSafetyOffset { get; set; }
    }

    /// <summary>
    /// Contains the validation rules for <see cref="SwarmHostDefaultOptions"/>.
    /// </summary>
    public class BackgroundJobSwarmHostDefaultOptionsValidationProfile : SwarmHostDefaultOptionsValidationProfile
    {
        /// <inheritdoc cref="SwarmHostDefaultOptionsValidationProfile"/>
        public BackgroundJobSwarmHostDefaultOptionsValidationProfile()
        {
            CreateValidationFor<BackgroundJobSwarmHostDefaultOptions>()
                .ForProperty(x => x.NotFoundCheckInterval)
                    .MustBeLargerOrEqualTo(1);
        }
    }
}
