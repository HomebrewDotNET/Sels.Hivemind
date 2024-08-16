using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <inheritdoc cref="IRecurringJobSettings"/>
    public class RecurringJobSettings : IRecurringJobSettings
    {
        /// <inheritdoc />
        public int? MaxRetryCount { get; set; }
        /// <inheritdoc />
        public TimeSpan[]? RetryTimes { get; set; }
        /// <inheritdoc />
        public bool? RetryUsingSchedule { get; set; }
        /// <inheritdoc />
        public ScheduleTime ScheduleTime { get; set; }
        /// <inheritdoc />
        public int MaxScheduleTries { get; set; }
        /// <inheritdoc />
        public bool AlwaysUseInterval { get; set; }
        /// <inheritdoc />
        public bool CanMisfire { get; set; }
        /// <inheritdoc />
        public MisfireBehaviour MisfireBehaviour { get; set; }
        /// <inheritdoc />
        public TimeSpan MisfireThreshold { get; set; }

    }
}
