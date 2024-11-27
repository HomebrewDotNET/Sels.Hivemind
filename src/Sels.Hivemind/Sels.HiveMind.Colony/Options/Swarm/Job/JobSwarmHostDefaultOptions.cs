using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Swarm.Job;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="JobSwarmHostOptions{TOptions}"/>.
    /// </summary>
    public class JobSwarmHostDefaultOptions : SwarmHostDefaultOptions
    {
        /// <summary>
        /// The default value for <see cref="JobSwarmHostOptions{TOptions}.MaxJobCommitTime"/>.
        /// </summary>
        public TimeSpan MaxJobCommitTime { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// The default value for <see cref="JobSwarmHostOptions{TOptions}.MaxNotFoundWaitTime"/>.
        /// </summary>
        public TimeSpan MaxNotFoundWaitTime { get; set; } = TimeSpan.FromMilliseconds(250);
        /// <summary>
        /// The default value for <see cref="JobSwarmHostOptions{TOptions}.NotFoundCheckInterval"/>.
        /// </summary>
        public int NotFoundCheckInterval { get; set; } = 5;
        /// <summary>
        /// The default value for <see cref="JobSwarmHostOptions{TOptions}.LockedDelay"/>.
        /// </summary>
        public TimeSpan LockedDelay { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// The default value for <see cref="JobSwarmHostOptions{TOptions}.MaxSaveTime"/>.
        /// </summary>
        public TimeSpan MaxSaveTime { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="SwarmHostDefaultOptions"/>.
    /// </summary>
    public class JobSwarmHostDefaultOptionsValidationProfile : SwarmHostDefaultOptionsValidationProfile
    {
        /// <inheritdoc cref="SwarmHostDefaultOptionsValidationProfile"/>
        public JobSwarmHostDefaultOptionsValidationProfile()
        {
            CreateValidationFor<JobSwarmHostDefaultOptions>()
                .ForProperty(x => x.NotFoundCheckInterval)
                    .MustBeLargerOrEqualTo(1);
        }
    }
}
