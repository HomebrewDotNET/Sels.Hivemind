using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Priority for an entity placed in a queue. A higher priority means jobs will dequeued first.
    /// </summary>
    public enum QueuePriority
    {
        /// <summary>
        /// Highest priority.
        /// </summary>
        Critical = 0,
        /// <summary>
        /// Above normal priority.
        /// </summary>
        High = 1,
        /// <summary>
        /// Default priority.
        /// </summary>
        Normal = 2,
        /// <summary>
        /// Below normal priority.
        /// </summary>
        Low = 3,
        /// <summary>
        /// Lowest priority.
        /// </summary>
        None = 4,
    }
}
