using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Represents a recurring job that can be modified.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IWriteableRecurringJob : IWriteableJob<ILockedRecurringJob, IRecurringJobChangeTracker, IRecurringJobState, IRecurringJobAction>, IReadOnlyRecurringJob
    {
        /// <summary>
        /// The expected time (in utc) after which the recurring job should be executed.
        /// </summary>
        public new DateTime? ExpectedExecutionDateUtc { get; set; }
        /// <summary>
        /// How many times the recurring job has been executed.
        /// </summary>
        public new long ExecutedAmount { get; set; }
        /// <summary>
        /// The last time (in utc) that the execution of the recurring job was started.
        /// </summary>
        public new DateTime? LastStartedDateUtc { get; set; }
        /// <summary>
        /// The last time (in utc) that the execution of the recurring job was completed.
        /// </summary>
        public new DateTime? LastCompletedDateUtc { get; set; }
    }
}
