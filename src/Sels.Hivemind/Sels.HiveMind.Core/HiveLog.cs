using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains the log parameters for HiveMind.
    /// </summary>
    public static class HiveLog
    {
        /// <summary>
        /// Contains the log parameters related background jobs.
        /// </summary>
        public static class BackgroundJob
        {
            /// <summary>
            /// Log parameter that contains the id of a background job.
            /// </summary>
            public const string Id = "{HiveMind.BackgroundJob.Id}";
            /// <summary>
            /// Log parameter that contains the queue of a background job.
            /// </summary>
            public const string Queue = "{HiveMind.BackgroundJob.Queue}";
            /// <summary>
            /// Log parameter that contains the priority of a background job.
            /// </summary>
            public const string Priority = "{HiveMind.BackgroundJob.Priority}";
            /// <summary>
            /// Log parameter that contains the current state name of a background job.
            /// </summary>
            public const string State = "{HiveMind.BackgroundJob.State}";
        }
    }
}
