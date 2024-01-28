using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Deletion
{
    /// <summary>
    /// The options used by a deletion swarm host.
    /// </summary>
    public interface IDeletionDaemonOptions : IBackgroundJobSwarmHostOptions<DeletionDeamonOptions>
    {
        /// <summary>
        /// How long to delay the dequeued job for when deletion fails for some reason.
        /// When set to null, the default value in <see cref="DeletionDaemonDefaultOptions"/> will be used.
        /// </summary>
        TimeSpan? ErrorDelay { get; }
        /// <summary>
        /// The maximum allowed deviation between the time a job is supposed to be deleted and when the job is scheduled for deletion.
        /// If the deviation is not within the allowed deviation the job will be delayed.
        /// When set to null, the default value in <see cref="DeletionDaemonDefaultOptions"/> will be used.
        /// </summary>
        TimeSpan? AllowedDeviation { get; }
        /// <summary>
        /// The multiplier that will be applied to <see cref="Environment.ProcessorCount"/> (rounded down) to get the amount of drones an auto managed deletion daemon can use.
        /// When set to null, the default value in <see cref="DeletionDaemonDefaultOptions"/> will be used.
        /// </summary>
        double? AutoManagedDroneCoreMultiplier { get; }
        /// <summary>
        /// How often an automanaged deletion daemon will restart to query the active queues of the system.
        /// When set to null, the default value in <see cref="DeletionDaemonDefaultOptions"/> will be used.
        /// </summary>
        TimeSpan? AutoManagedRestartInterval { get; }
        /// <summary>
        /// True if the swarm is auto managed, otherwise false if the swarm options are manually set.
        /// Auto managed swarms will manage some settings on their own (Mainly the swarm structure, number of drones, scheduler and the queues)
        /// </summary>
        bool IsAutoManaged { get; }
    }
}
