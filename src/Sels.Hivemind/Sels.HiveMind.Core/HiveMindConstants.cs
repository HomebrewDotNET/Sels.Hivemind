using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;

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
            /// <summary>
            /// The maximum allowed size for <see cref="StorageType.Text"/> properties. Anything larger than this value will be stored as <see cref="StorageType.Serialized"/>.
            /// </summary>
            public const int TextTypeMaxSize = 1000;
        }

        /// <summary>
        /// Contains constant/static read only properties related to HiveMind queues.
        /// </summary>
        public static class Queue
        {
            /// <summary>
            /// The default queue name that will be used when none is specified when queueing new jobs.
            /// </summary>
            public const string DefaultQueue = "Global";

            /// <summary>
            /// The name of the queue type that contains the background job to execute.
            /// </summary>
            public const string BackgroundJobProcessQueueType = "$BackgroundJob.Process";
            /// <summary>
            /// The name of the queue type that contains the recurring jobs to execute.
            /// </summary>
            public const string RecurringJobTriggerQueueType = "$RecurringJob.Trigger";
            /// <summary>
            /// The name of the queue type that contains the jobs to cleanup. (delete, archive, ...)
            /// </summary>
            public const string BackgroundJobCleanupQueueType = "$BackgroundJob.Cleanup";
        }

        /// <summary>
        /// Contains constant/static read only properties related to background jobs.
        /// </summary>
        public static class Job
        {
            /// <summary>
            /// Contains the names of common properties set on jobs.
            /// </summary>
            public static class Properties
            {
                /// <summary>
                /// Contains the name of the machine that created the job.
                /// </summary>
                public const string SourceMachineName = "$SourceMachine.Name";
                /// <summary>
                /// Contains the platform of the machine that created the job.
                /// </summary>
                public const string SourceMachinePlatform = "$SourceMachine.Platform";
                /// <summary>
                /// Contains the process architecture of the machine that created the job.
                /// </summary>
                public const string SourceMachineArchitecture= "$SourceMachine.Architecture";
                /// <summary>
                /// Contains the name of the user that created the job.
                /// </summary>
                public const string SourceUserName = "$SourceUser.Name";
                /// <summary>
                /// Contains the domain of the user that created the job.
                /// </summary>
                public const string SourceUserDomain = "$SourceUser.Domain";
                /// <summary>
                /// Contains the culture of the thread that created the job.
                /// </summary>
                public const string ThreadCulture = "$Thread.Culture";
                /// <summary>
                /// Contains the UI culture of the thread that created the job.
                /// </summary>
                public const string ThreadUiCulture = "$Thread.UiCulture";

                /// <summary>
                /// Contains the current retry count of a background job.
                /// </summary>
                public const string RetryCount = "$Execution.RetryCount";
                /// <summary>
                /// Contains the total amount of times a job has been retried.
                /// </summary>
                public const string TotalRetryCount = "$Execution.TotalRetryCount";
                /// <summary>
                /// True if cleanup was triggered for the job, otherwise false.
                /// </summary>
                public const string CleanupTriggered = "$Cleanup.Triggered";
                /// <summary>
                /// Contains the time how long the background jobs will kept after being completed.
                /// </summary>
                public const string CleanupRetention = "$Cleanup.Retention";
            }
        }
    }
}
