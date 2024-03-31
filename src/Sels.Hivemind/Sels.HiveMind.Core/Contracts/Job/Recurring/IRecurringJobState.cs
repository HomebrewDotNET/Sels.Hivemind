using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a state that a <see cref="IReadOnlyRecurringJob"/> can be in.
    /// </summary>
    public interface IRecurringJobState : IJobState
    {
        /// <summary>
        /// Represents a unique sequence number for the state. 
        /// Sequence is increased each time a rcurring job transitions into a new state.
        /// </summary>
        public long Sequence { get; set; }
    }
}
