using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Contains the settings for a recurring job.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobSettings
    {
        // Retry
        /// <summary>
        /// The maximum amount of times the recurring job can be retried.
        /// When set to null the values from <see cref="RecurringJobRetryOptions"/> will be used.
        /// </summary>
        public int? MaxRetryCount { get; }
        /// <summary>
        /// Contains how long to wait before retrying the recurring job. 
        /// Element is taken based on the current retry count of the job.
        /// When set to null the values from <see cref="RecurringJobRetryOptions"/> will be used.
        /// </summary>
        public TimeSpan[]? RetryTimes { get;  }
        /// <summary>
        /// Can be used to overwrite the default retry behavior. Instead of using the retry times, the next execution date will be determined by using the recurring job schedule.
        /// Set to true to use the schedule or false to use the default retry behavior.
        /// When set to null the values from <see cref="RecurringJobRetryOptions"/> will be used.
        /// </summary>
        public bool? RetryUsingSchedule { get; }

        // Schedule
        /// <inheritdoc cref="Sels.HiveMind.Job.ScheduleTime"/>
        public ScheduleTime ScheduleTime { get; }
        /// <summary>
        /// If the interval should always be used to generate the next. Only used when calendars are also defined. 
        /// When set to false the dates determined by the calendars are also considered valid schedule dates, when set to true the interval is applied on dates determined by the calendars.
        /// </summary>
        public bool AlwaysUseInterval { get; }
        /// <summary>
        /// How many times the recurring job will attempt to generate the next schedule date. Used to avoid infinite loops when the schedule is invalid.
        /// </summary>
        public int MaxScheduleTries { get; }

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

        // Retention
        /// <summary>
        /// Defines when older states should be removed.
        /// </summary>
        public RecurringJobRetentionMode StateRetentionMode { get; }
        /// <summary>
        /// The amount that will be applied based on the selected option in <see cref="StateRetentionMode"/>.
        /// For <see cref="RecurringJobRetentionMode.Amount"/> this will be the amount of states that will be kept.
        /// For <see cref="RecurringJobRetentionMode.OlderThan"/> this will be the amount of days that will be kept.
        /// </summary>
        public int StateRetentionAmount { get; }
        /// <summary>
        /// Defines when older logs should be removed.
        /// </summary>
        public RecurringJobRetentionMode LogRetentionMode { get; }
        /// <summary>
        /// The amount that will be applied based on the selected option in <see cref="LogRetentionMode"/>.
        /// For <see cref="RecurringJobRetentionMode.Amount"/> this will be the amount of logs that will be kept.
        /// For <see cref="RecurringJobRetentionMode.OlderThan"/> this will be the amount of days that will be kept.
        /// </summary>
        public int LogRetentionAmount { get; }
    }
}
