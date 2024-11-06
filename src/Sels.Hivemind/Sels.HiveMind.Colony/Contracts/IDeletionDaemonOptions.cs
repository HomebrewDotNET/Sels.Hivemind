using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Colony.Options;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Events.Job.Background;

using Sels.HiveMind.Storage;
namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// The options used by a deletion swarm host.
    /// </summary>
    public interface IDeletionDaemonOptions : IBackgroundJobQueueProcessorOptions
    {
        /// <summary>
        /// How many colonies are allowed to trigger bulk deletions when <see cref="DeletionMode"/> is set to <see cref="DeletionMode.Bulk"/>.
        /// Zero or less means each node can trigger a bulk deletion.
        /// </summary>
        public int MaxConcurrentBulkDeletions { get; set; }
        /// <summary>
        /// How many jobs will be deleted in a single transaction.
        /// Should be kept small to avoid long running transactions and potentially block other processes.
        /// </summary>
        public int BulkDeletionBatchSize { get; set; }
    }
}
