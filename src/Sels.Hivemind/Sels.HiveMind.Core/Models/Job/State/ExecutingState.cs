using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Job is being processed by a drone.
    /// </summary>
    public class ExecutingState : BaseSharedJobState<ExecutingState>
    {
        // Properties
        /// <summary>
        /// The colony the job is executing on.
        /// </summary>
        public string Colony { get; }
        /// <summary>
        /// The swarm in <see cref="Colony"/> the job is being executed by.
        /// </summary>
        public string Swarm { get; }
        /// <summary>
        /// The drone in <see cref="Swarm"/> managing the execution of the job.
        /// </summary>
        public string Drone { get; }

        /// <inheritdoc cref="ExecutingState"/>
        /// <param name="colony"><inheritdoc cref="Colony"/></param>
        /// <param name="swarm"><inheritdoc cref="Swarm"/></param>
        /// <param name="drone"><inheritdoc cref="Drone"/></param>
        public ExecutingState(string colony, string swarm, string drone)
        {
            Colony = colony.ValidateArgumentNotNullOrWhitespace(nameof(colony));
            Swarm = swarm.ValidateArgumentNotNullOrWhitespace(nameof(swarm));
            Drone = drone.ValidateArgumentNotNullOrWhitespace(nameof(drone));
        }
    }
}
