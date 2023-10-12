using Sels.Core.Extensions;
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
        /// </summary>
        public string[] ValidStates { get; }

        /// <inheritdoc cref="AwaitingState"/>
        /// <param name="jobId"><inheritdoc cref="JobId"/></param>
        /// <param name="validStates"><inheritdoc cref="ValidStates"/></param>
        public AwaitingState(string jobId, IEnumerable<string> validStates)
        {
            JobId = jobId.ValidateArgument(nameof(jobId));
            ValidStates = validStates.ValidateArgumentNotNullOrEmpty(nameof(validStates)).ToArray();
        }
    }
}
