using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Models.Storage.Schedule;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Job.Recurring
{
    /// <summary>
    /// Contains the state needed to create a recurring job transformed into a format for storage.
    /// </summary>
    public class RecurringJobConfigurationStorageData
    {
        /// <summary>
        /// The id of the recurring job to create.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The queue the recurring job should be placed in.
        /// </summary>
        public string Queue { get; set; }
        /// <summary>
        /// The priority of the recurring job in <see cref="Queue"/>.
        /// </summary>
        public QueuePriority Priority { get; set; }
        /// <summary>
        /// Who is requesting the creation or update. Used as the initial lock holder.
        /// </summary>
        public string Requester { get; set; }

        /// <summary>
        /// Data about how to execute the job transformed into a format for storage.
        /// </summary>
        public InvocationStorageData InvocationData { get; set; }

        /// <summary>
        /// The schedule that will be used to determine the next time the job needs to be executed transformed into a format for storage.
        /// </summary>
        public ScheduleStorageData Schedule { get; set; }

        /// <summary>
        /// The settings assigned to the recurring job.
        /// </summary>
        public RecurringJobSettings Settings { get; set; }

        /// <summary>
        /// Any middleware to execute for the job transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<MiddlewareStorageData> Middleware { get; set; }

        /// <summary>
        /// The creation date (in utc) that will be set on the new job if it is created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// The modification date (in utc) that will be set on job if it is created.
        /// </summary>
        public DateTime ModifedAt => CreatedAt;
    }
}
