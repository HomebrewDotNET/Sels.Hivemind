using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Indicates the features that a <see cref="IJobQueue"/> supports.
    /// </summary>
    [Flags]
    public enum JobQueueFeatures
    {
        /// <summary>
        /// Job queue supports polling using <see cref="IJobQueue.DequeueAsync(string, IEnumerable{string}, int, System.Threading.CancellationToken)"/>.
        /// </summary>
        Polling = 1,
        /// <summary>
        /// Job queue supports subscribing using TODO.
        /// </summary>
        Subscription = 2,
    }
}
