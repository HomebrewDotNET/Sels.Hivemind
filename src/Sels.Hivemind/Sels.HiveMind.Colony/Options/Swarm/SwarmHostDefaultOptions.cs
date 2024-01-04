using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="SwarmHostOptions{TOptions}"/>.
    /// </summary>
    public class SwarmHostDefaultOptions
    {
        /// <summary>
        /// The default amount of drones to use for root swarmes.
        /// </summary>
        public int RootSwarmDroneAmount { get; set; } = Environment.ProcessorCount;
        /// <summary>
        /// The default amount of drones to use for sub swarmes.
        /// </summary>
        public int SubSwarmDroneAmount { get; set; } = 1;
        /// <summary>
        /// The default scheduler type to use when using a job queue that supports polling.
        /// </summary>
        public string PollingSchedulerType { get; set; } = HiveMindConstants.Scheduling.LazyType;
        /// <summary>
        /// The default scheduler type to use when using a job queue that supports subscription.
        /// </summary>
        public string SubscriptionSchedulerType { get; set; } = HiveMindConstants.Scheduling.SubscriptionType;
        /// <summary>
        /// The default time how long to allow drones to finish their current job before forcefully cancelling the processing.
        /// </summary>
        public TimeSpan GracefulStoptime { get; } = TimeSpan.FromSeconds(5);
        /// <summary>
        /// The default maximum amount of time to wait for drones to stop processing to avoid blocking forever.
        /// </summary>
        public TimeSpan MaxStoptime { get; } = TimeSpan.FromSeconds(25);
        /// <summary>
        /// The default time a drone will sleep when it threw an unhandled exception.
        /// </summary>
        public TimeSpan UnhandledExceptionSleepTime { get; } = TimeSpan.FromSeconds(5);
        /// <summary>
        /// The default time before a lock is supposed to expire to set the heartbeat. Only used for queues that don't manage the locks themselves.
        /// </summary>
        public TimeSpan LockExpirySafetyOffset { get; } = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="SwarmHostDefaultOptions"/>.
    /// </summary>
    public class SwarmHostDefaultOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="SwarmHostDefaultOptionsValidationProfile"/>
        public SwarmHostDefaultOptionsValidationProfile()
        {
            CreateValidationFor<SwarmHostDefaultOptions>()
                .ForProperty(x => x.RootSwarmDroneAmount)
                    .MustBeLargerOrEqualTo(0)
                .ForProperty(x => x.SubSwarmDroneAmount)
                    .MustBeLargerOrEqualTo(0)
                .ForProperty(x => x.PollingSchedulerType)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.SubscriptionSchedulerType)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.MaxStoptime)
                    .ValidIf(x => x.Value > x.Source.GracefulStoptime, x => $"Must be larger than <{nameof(x.Source.GracefulStoptime)}>");
        }
    }
}
