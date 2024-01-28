using Sels.HiveMind.Queue.Sql;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue.MySql
{
    /// <summary>
    /// Table that contains enqueued jobs in a MySql table.
    /// </summary>
    public class MySqlJobQueueTable : JobQueueTable
    {
        /// <summary>
        /// The id of the process that has a lock on a job. 
        /// </summary>
        public string ProcessId { get; set; }   
    }
}
