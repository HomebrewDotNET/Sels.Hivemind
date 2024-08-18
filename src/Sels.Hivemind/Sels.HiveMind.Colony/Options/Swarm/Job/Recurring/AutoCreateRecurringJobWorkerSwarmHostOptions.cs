using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.Recurring
{
    /// <summary>
    /// Contains the options that will be used to create an auto managed <see cref="RecurringJobWorkerSwarmHost"/> when <see cref="HiveColonyCreationOptions.AutoCreateRecurringJobWorkerSwarmHost"/> is enabled.
    /// </summary>
    public class AutoCreateRecurringJobWorkerSwarmHostOptions
    {
        /// <summary>
        /// The name that will be used for the created daemon.
        /// </summary>
        public string Name { get; set; } = "$System.RecurringJobSwarmHost";
        /// <summary>
        /// The multiplier that will be applied to <see cref="Environment.ProcessorCount"/> (rounded down) to get the amount of drones that can be used.
        /// </summary>
        public double AutoManagedDroneCoreMultiplier { get; set; } = 0.2;
        /// <summary>
        /// How many drones will be used to process recurring jobs. When set to null the amount of drones will be determined by <see cref="AutoManagedDroneCoreMultiplier"/>.
        /// </summary>
        public int? Drones { get; set; }
        /// <summary>
        /// How often the daemon will check for new recurring job queues to process.
        /// </summary>
        public TimeSpan QueuePollingInterval { get; set; } = TimeSpan.FromMinutes(15);
        /// <summary>
        /// Optional groups that can be defined to either restrict the queues that the daemon will process or assign different priorities based on the queue prefix.
        /// When null or empty all queues will be monitored.
        /// </summary>
        public List<QueueSubscriptionGroup> QueueSubscriptions { get; set; }
    }

    /// <summary>
    /// Contains the queue prefix that an <see cref="AutoManagedRecurringJobWorkerSwarmHost"/> will monitor.
    /// </summary>
    public class QueueSubscriptionGroup
    {
        /// <summary>
        /// Prefix that is used to restrict queues processed by the daemon that only starts with the prefix and/or with <see cref="Priority"/>.
        /// </summary>
        public string Prefix { get; set; }
        /// <summary>
        /// The priority that will be assigned to the queues matching the current group.
        /// </summary>
        public byte? Priority { get; set; }
    }

    /// <summary>
    /// Contains the validation rules for <see cref="AutoCreateRecurringJobWorkerSwarmHostOptions"/>.
    /// </summary>
    public class AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile : ValidationProfile<string>
    {
        // Statics
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile Instance { get; } = new AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile();

        private AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile()
        {
            CreateValidationFor<AutoCreateRecurringJobWorkerSwarmHostOptions>()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.AutoManagedDroneCoreMultiplier)
                    .MustBeLargerOrEqualTo(0d)
                    .MustBeSmallerOrEqualTo(1d)
                .ForProperty(x => x.Drones, x => x!.Value)
                    .MustBeLargerOrEqualTo(1);

            CreateValidationFor<QueueSubscriptionGroup>()
                .ForProperty(x => x.Prefix)
                    .CannotBeNullOrWhitespace();
        }
    }
}
