using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Contains the settings for a recurring job.
    /// </summary>
    public interface IRecurringJobSettings
    {
        // Retry
        /// <summary>
        /// The maximum amount of times the recurring job can be retried.
        /// </summary>
        public int MaxRetryCount { get; }
        /// <summary>
        /// Contains how long to wait before retrying the recurring job. 
        /// Element is taken based on the current retry count of the job.
        /// </summary>
        public TimeSpan[] RetryTimes { get;  }
        /// <summary>
        /// Can be used to overwrite the default retry behavior. Instead of using the retry times, the next execution date will be determined by using the recurring job schedule.
        /// Set to true to use the schedule or false to use the default retry behavior.
        /// </summary>
        public bool RetryUsingSchedule { get;  }

        // Schedule
        /// <summary>
        /// <inheritdoc cref="ScheduleTime"/>
        /// </summary>
        public ScheduleTime ScheduleTime { get; }

        // Misfire
        /// <summary>
        /// Determines if the recurring job can misfire.
        /// A recurring job will misfire when the latency between the expected execution time and the actual execution time is greater than the misfire threshold.
        /// When set to false the recurring job will always be executed.
        /// </summary>
        public bool CanMisfire { get; }
        /// <summary>
        /// <inheritdoc cref="Sels.HiveMind.Job.MisfireBehaviour"/>
        /// </summary>
        public MisfireBehaviour MisfireBehaviour { get; }
        /// <summary>
        /// The maximum amount of allowed latency. Only used when <see cref="CanMisfire"/> is set to true.
        /// </summary>
        public TimeSpan MisfireThreshold { get; }
    }
}
