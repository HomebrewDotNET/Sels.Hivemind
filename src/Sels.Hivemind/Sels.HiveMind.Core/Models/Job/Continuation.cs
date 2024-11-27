using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Contains the criteria for enqueueing an awaiting job when the job this continuation is attached to changes state.
    /// </summary>
    public class Continuation
    {
        /// <summary>
        /// The id of the job that is awaiting the current job.
        /// </summary>
        public string AwaitingJobId { get; set; }
        /// <summary>
        /// The names of the states after which the awaiting job can move to the <see cref="EnqueuedState"/>.
        /// When null all states will be valid.
        /// </summary>
        public string[] ValidStates { get; set; }
        /// <summary>
        /// If the awaiting job needs to be deleted if the current job transitions into another state that is not in <see cref="ValidStates"/>.
        /// When set to false the job will stay in the awaiting state.
        /// </summary>
        public bool DeleteOnOtherState { get; }
        /// <summary>
        /// How much to delay the awaiting job by when it is moved to <see cref="EnqueuedState"/>.
        /// </summary>
        public TimeSpan? DelayBy { get; set; }
        /// <summary>
        /// True if <see cref="WasTriggered"/> was enqueued, otherwise false.
        /// </summary>
        public bool WasTriggered { get; set; }
    }
}
