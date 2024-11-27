using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    public interface IBackgroundJobQueueProcessorOptions
    {
        /// <summary>
        /// The multiplier that will be applied to <see cref="Environment.ProcessorCount"/> (rounded down) to get the amount of drones that can be used.
        /// </summary>
        double AutoManagedDroneCoreMultiplier { get; }
        /// <summary>
        /// How many threads will be used to process background jobs. When set to null the amount of threads will be determined by <see cref="AutoManagedDroneCoreMultiplier"/>.
        /// </summary>
        int? Drones { get; }
        /// <summary>
        /// How many jobs will be queried and locked in one request. Setting this too low can put more strain on the backing storage. Setting this too high can cause jobs to timeout.
        /// </summary>
        int DequeueSize { get; }
        /// <summary>
        /// The % threshold below which the deletion deamon can start prefetching jobs to delete. 
        /// </summary>
        double EarlyFetchThreshold { get; }
        /// <summary>
        /// The minimum amount of jobs needed after the first batch to avoid loops where only a small amount of jobs are deleted.
        /// </summary>
        int MininumBatchSize { get; }
        /// <summary>
        /// How many jobs will be processed in one batch by a drone. Used to reduce the amount of calls to the backing storage.
        /// </summary>
        int BatchSize { get; }
    }
}
