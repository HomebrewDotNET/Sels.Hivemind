using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to the table that contains the processing logs of a background job.
    /// </summary>
    public class BackgroundJobLogTable : LogEntry
    {
        /// <summary>
        /// The id of the background job the log is attached to.
        /// </summary>
        public long BackgroundJobId { get; set; }
    }
}
