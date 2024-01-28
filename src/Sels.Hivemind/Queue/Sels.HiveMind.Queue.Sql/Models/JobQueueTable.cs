using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue.Sql
{
    /// <summary>
    /// Table that contains enqueued jobs.
    /// </summary>
    public class JobQueueTable
    {
        /// <summary>
        /// The primary key of the enqueued job.
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// The type of the queue.
        /// </summary>
        public string Type { get; set; }    
        /// <summary>
        /// The name of the queue.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The if of the enqueued job.
        /// </summary>
        public string JobId { get; set; }
        /// <summary>
        /// The priority of the job.
        /// </summary>
        public QueuePriority Priority { get; set; }
        /// <summary>
        /// The execution id of the job when it was enqueued.
        /// </summary>
        public string ExecutionId { get; set; }
        /// <summary>
        /// The date (in utc) after which the job can be dequeued.
        /// </summary>
        public DateTime QueueTime { get; set; }
        /// <summary>
        /// When the job was fetched for processing. Null when still in queue.
        /// </summary>
        public DateTime? FetchedAt { get; set; }
        /// <summary>
        /// When the job was enqueued.
        /// </summary>
        public DateTime EnqueuedAt { get; set; }
    }
}
