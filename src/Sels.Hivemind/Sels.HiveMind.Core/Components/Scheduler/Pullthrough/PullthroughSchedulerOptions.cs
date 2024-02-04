using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Exposes extra options for <see cref="PullthroughScheduler"/>.
    /// </summary>
    public class PullthroughSchedulerOptions
    {
        /// <summary>
        /// Multiplier used to determine the amount of jobs to fetch for each level of concurrency. 
        /// (e.g. Concurrency of 5 and a prefetch 1: Will fetch 1 job when requested and will fetch up to 5 jobs when monitoring empty queue)
        /// (e.g. Concurrency of 5 and a prefetch 3: Will fetch 3 jobs when requested and will fetch up to 15 jobs when monitoring empty queue)
        /// </summary>
        public int PrefetchMultiplier { get; set; } = 1;
        /// <summary>
        /// How long to sleep for when all queues are empty.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(15);
        /// <summary>
        /// How long to wait before checking an empty queue again. Higher value means better performance when using a complex queue configuration (Multiple sub swarms and priorities) but increases the potential latency when new work is scheduled on an empty queue.
        /// </summary>
        public TimeSpan EmptyQueueCheckDelay { get; set; } = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="PullthroughSchedulerOptions"/>
    /// </summary>
    public class LazySchedulerOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="PullthroughSchedulerOptions"/>
        public LazySchedulerOptionsValidationProfile()
        {
            CreateValidationFor<PullthroughSchedulerOptions>()
                .ForProperty(x => x.PrefetchMultiplier)
                    .MustBeLargerOrEqualTo(1);
        }
    }
}
