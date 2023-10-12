using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains constant/static read only properties related to HiveMind.
    /// </summary>
    public static class HiveMindConstants
    {
        /// <summary>
        /// The default environment used by HiveMind.
        /// </summary>
        public const string DefaultEnvironmentName = "Main";

        /// <summary>
        /// Contains constant/static read only properties related to HiveMind storage.
        /// </summary>
        public static class Storage
        {

        }

        /// <summary>
        /// Contains constant/static read only properties related to HiveMind queues.
        /// </summary>
        public static class Queue
        {
            /// <summary>
            /// The name of the queue type that contains the background job to execute.
            /// </summary>
            public const string BackgroundJobProcessQueueType = "System.BackgroundJob.Process";
            /// <summary>
            /// The name of the queue type that contains the recurring jobs to execute.
            /// </summary>
            public const string RecurringJobProcessQueueType = "System.RecurringJob.Process";
            /// <summary>
            /// The name of the queue type that contains the jobs to cleanup. (delete, archive, ...)
            /// </summary>
            public const string BackgroundJobCleanupQueueType = "System.BackgroundJob.Cleanup";
        }

        /// <summary>
        /// Contains constant/static read only properties related to background jobs.
        /// </summary>
        public static class Job
        {
            /// <summary>
            /// The default queue name that will be used when none is specified when queueing new jobs.
            /// </summary>
            public const string DefaultQueue = "Global";
        }
    }
}
