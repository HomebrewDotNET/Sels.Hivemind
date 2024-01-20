using Sels.Core.Extensions;
using Sels.Core.Extensions.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Job is waiting for another job to transition into a certain state before moving into the <see cref="EnqueuedState"/>.
    /// </summary>
    public class AwaitingState : BaseBackgroundJobState<AwaitingState>
    {
        // Properties
        /// <summary>
        /// The id of the job to wait for.
        /// </summary>
        public string JobId { get; }
        /// <summary>
        /// The names of the states after which the job can move to the <see cref="EnqueuedState"/>.
        /// When null all states will be valid.
        /// </summary>
        public string[] ValidStates { get; }
        /// <summary>
        /// If the current job needs to be deleted if job <see cref="JobId"/> transitions into another state that is not in <see cref="ValidStates"/>.
        /// When set to false the job will stay in the awaiting state.
        /// </summary>
        public bool DeleteOnOtherState { get; }
        /// <summary>
        /// How much to delay the job by when it is moved to <see cref="EnqueuedState"/>.
        /// </summary>
        public TimeSpan? DelayBy { get; set; }


        /// <inheritdoc cref="AwaitingState"/>
        /// <param name="jobId"><inheritdoc cref="JobId"/></param>
        /// <param name="validStates"><inheritdoc cref="ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="DeleteOnOtherState"/></param>
        public AwaitingState(string jobId, IEnumerable<string> validStates, bool deleteOnOtherState)
        {
            JobId = jobId.ValidateArgument(nameof(jobId));
            ValidStates = validStates.HasValue() ? validStates.ToArray() : null;
            DeleteOnOtherState = deleteOnOtherState;
        }
    }
}
