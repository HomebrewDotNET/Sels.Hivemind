using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the unqueryable data attached to a background job.
    /// </summary>
    public class RecurringJobDataTable
    {
        /// <summary>
        /// The id of the recurring job this data is attached to.
        /// </summary>
        public string RecurringJobId { get; set; }
    }
}
