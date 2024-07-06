using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
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
    }
}
