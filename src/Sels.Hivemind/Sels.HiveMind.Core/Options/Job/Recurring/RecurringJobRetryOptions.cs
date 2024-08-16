using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Options for configuring the setting for retrying failed background jobs.
    /// </summary>
    public class RecurringJobRetryOptions : JobRetryOptions
    {
        /// <summary>
        /// Can be used to overwrite the default retry behavior. Instead of using the retry times, the next execution date will be determined by using the recurring job schedule.
        /// Set to true to use the schedule or false to use the default retry behavior.
        /// </summary>
        public bool RetryUsingSchedule { get; set; }
    }
}
