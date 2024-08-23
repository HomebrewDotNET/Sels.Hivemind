using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Defines the retention method that should be used on recurring job related data.
    /// </summary>
    public enum RecurringJobRetentionMode
    {
        /// <summary>
        /// Nothing will be removed. Can cause storage to grow indefinitely.
        /// </summary>
        KeepAll = 0,
        /// <summary>
        /// Data will be removed based on a configured time value.
        /// </summary>
        OlderThan = 1,
        /// <summary>
        /// Data will be removed when the total size reaches a certain threshold. Data will be removed until the threshold is reached.
        /// </summary>
        Amount = 2
    }
}
