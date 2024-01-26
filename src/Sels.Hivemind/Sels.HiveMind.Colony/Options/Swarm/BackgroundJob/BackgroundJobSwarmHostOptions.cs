using Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob
{
    /// <summary>
    /// Contains the common configuration options for a swarm host.
    /// </summary>
    /// <typeparam name="TOptions">The type of inhereting this class</typeparam>
    public abstract class BackgroundJobSwarmHostOptions<TOptions> : SwarmHostOptions<TOptions>, IBackgroundJobSwarmHostOptions<TOptions>
        where TOptions : BackgroundJobSwarmHostOptions<TOptions>
    {
        /// <inheritdoc/>
        public TimeSpan? MaxJobCommitTime { get; set; }
        /// <inheritdoc/>
        public TimeSpan? MaxNotFoundWaitTime { get; set; }
        /// <inheritdoc/>
        public int? NotFoundCheckInterval { get; set; }
        /// <inheritdoc/>
        public TimeSpan? LockedDelay { get; set; }
        /// <inheritdoc/>
        public TimeSpan? MaxSaveTime { get; set; }
    }
    /// <summary>
    /// Contains the validation rules for <see cref="BackgroundJobSwarmHostOptions{TOptions}"/>.
    /// </summary>
    public abstract class BackgroundJobSwarmHostOptionsValidationProfile<TOptions> : SwarmHostOptionsValidationProfile<TOptions>
        where TOptions : BackgroundJobSwarmHostOptions<TOptions>
    {
        /// <inheritdoc cref="BackgroundJobSwarmHostOptionsValidationProfile{TOptions}"/>
        public BackgroundJobSwarmHostOptionsValidationProfile() : base()
        {
            CreateValidationFor<BackgroundJobSwarmHostOptions<TOptions>>()
                .ForProperty(x => x.NotFoundCheckInterval, x => x.Value)
                    .MustBeLargerOrEqualTo(1);
        }
    }
}
