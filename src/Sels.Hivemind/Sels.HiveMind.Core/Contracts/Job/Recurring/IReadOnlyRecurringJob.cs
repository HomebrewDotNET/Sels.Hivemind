using Sels.HiveMind.Schedule;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Represents a read-only recurring job with it's current state.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IReadOnlyRecurringJob : IReadOnlyJob<ILockedRecurringJob, IRecurringJobChangeTracker, IRecurringJobState, IRecurringJobAction>
    {
        // Properties

        /// <summary>
        /// The schedule that will be used to determine the next time the job needs to be executed.
        /// </summary>
        public ISchedule Schedule { get; }
        /// <summary>
        /// The settings configured for this recurring job.
        /// </summary>
        public IRecurringJobSettings Settings { get; }

        /// <summary>
        /// The expected time (in utc) after which the recurring job should be executed.
        /// </summary>
        public DateTime? ExpectedExecutionDateUtc { get; }
        /// <summary>
        /// The expected time (local machine) after which the recurring job should be executed.
        /// </summary>
        public DateTime? ExpectedExecutionDate => ExpectedExecutionDateUtc?.ToLocalTime();
        /// <summary>
        /// How many times the recurring job has been executed.
        /// </summary>
        public long ExecutedAmount { get; }
        /// <summary>
        /// The last time (in utc) that the execution of the recurring job was started.
        /// </summary>
        public DateTime? LastStartedDateUtc { get; }
        /// <summary>
        /// The last time (local machine) that the execution of the recurring job was started.
        /// </summary>
        public DateTime? LastStartedDate => LastStartedDateUtc?.ToLocalTime();
        /// <summary>
        /// The last time (in utc) that the execution of the recurring job was completed.
        /// </summary>
        public DateTime? LastCompletedDateUtc { get; }
        /// <summary>
        /// The last time (local machine) that the execution of the recurring job was completed.
        /// </summary>
        public DateTime? LastCompletedDate => LastCompletedDateUtc?.ToLocalTime();
    }
}
