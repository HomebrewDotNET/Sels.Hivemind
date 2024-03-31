using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the processing logs of a recurring job.
    /// </summary>
    public class RecurringJobLogTable : LogEntry
    {
        /// <summary>
        /// The id of the recurring job the log is attached to.
        /// </summary>
        public long RecurringJobId { get; set; }
    }
}
