using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Swarm.Job;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// Contains the common configuration options for a swarm host.
    /// </summary>
    /// <typeparam name="TSelf">The top level type inheriting from this class</typeparam>
    /// <typeparam name="TOptions">The public readonly type of the current options</typeparam>
    public abstract class JobSwarmHostOptions<TSelf, TOptions> : SwarmHostOptions<TSelf, TOptions>, IJobSwarmHostOptions<TOptions>
        where TSelf : TOptions
        where TOptions : IJobSwarmHostOptions<TOptions>
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
    /// Contains the validation rules for <see cref="JobSwarmHostOptions{TSelf, TOptions}"/>.
    /// </summary>
    public abstract class JobSwarmHostOptionsValidationProfile<TSelf, TOptions> : SwarmHostOptionsValidationProfile<TSelf, TOptions>
        where TSelf : TOptions
        where TOptions : IJobSwarmHostOptions<TOptions>
    {
        /// <inheritdoc cref="JobSwarmHostOptionsValidationProfile{TOptions}"/>
        public JobSwarmHostOptionsValidationProfile() : base()
        {
            CreateValidationFor<JobSwarmHostOptions<TSelf, TOptions>>()
                .ForProperty(x => x.NotFoundCheckInterval, x => x!.Value)
                    .MustBeLargerOrEqualTo(1);
        }
    }
}
