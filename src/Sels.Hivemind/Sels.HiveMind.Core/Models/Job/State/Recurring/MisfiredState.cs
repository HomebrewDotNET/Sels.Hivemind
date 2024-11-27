using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.State.Recurring
{
    /// <summary>
    /// State that indicates that a recurring job misfired.
    /// </summary>
    public class MisfiredState : BaseRecurringJobState<MisfiredState>
    {
        /// <summary>
        /// The total latency between the expected execution date and when the recurring job was actually preparing to execute.
        /// </summary>
        public TimeSpan Latency { get; }

        /// <inheritdoc cref="MisfiredState"/>
        /// <param name="latency"><inheritdoc cref="Latency"/></param>
        public MisfiredState(TimeSpan latency)
        {
            Latency = latency;
        }
    }
}
