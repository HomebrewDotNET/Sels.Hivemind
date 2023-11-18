using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Scheduler.Lazy
{
    /// <summary>
    /// Exposes extra options for <see cref="LazyScheduler"/>.
    /// </summary>
    public class LazySchedulerOptions
    {
        /// <summary>
        /// Multiplier used to determine the amount of jobs to fetch for each level of concurrency. 
        /// (e.g. Concurrency of 5 and a prefetch 1: Will fetch 1 job when requested and will fetch up to 5 jobs when monitoring empty queue)
        /// (e.g. Concurrency of 5 and a prefetch 3: Will fetch 3 jobs when requested and will fetch up to 15 jobs when monitoring empty queue)
        /// </summary>
        public int PrefetchMultiplier { get; set; } = 1;
        /// <summary>
        /// How often to check the queues for new jobs when they were empty.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="LazySchedulerOptions"/>
    /// </summary>
    public class LazySchedulerOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="LazySchedulerOptions"/>
        public LazySchedulerOptionsValidationProfile()
        {
            CreateValidationFor<LazySchedulerOptions>()
                .ForProperty(x => x.PrefetchMultiplier)
                    .MustBeLargerOrEqualTo(1)
                .ForProperty(x => x.PollingInterval)
                    .MustBeLargerOrEqualTo(TimeSpan.FromMilliseconds(100));
        }
    }
}
