using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Scheduler;
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
            public const int TextTypeMaxSize = 255;
        }

        /// <summary>
        /// Contains constant/static read only properties related to scheduling jobs.
        /// </summary>
        public static class Scheduling
        {
            /// <summary>
            /// The type of the lazy scheduler.
            /// </summary>
            public static string PullthoughType = PullthroughScheduler.SchedulerType;
            /// <summary>
            /// The type of the producing scheduler.
            /// </summary>
            public const string ProducingType = "Producing";
            /// <summary>
            /// The type of the simple scheduler.
            /// </summary>
            public const string SimpleType = "Simple";
            /// <summary>
            /// The type of the subscription scheduler.
            /// </summary>
            public const string SubscriptionType = "Subscription";
        }

        /// <summary>
        /// Contains constant/static read only properties related to intervals.
        /// </summary>
        public static class Intervals
        {
            /// <summary>
            /// The type of the interval based on <see cref="TimeSpan"/>.
            /// </summary>
            public static string TimeType = TimeInterval.Type;
        }

        /// <summary>
        /// Contains constant/static read only properties related querying HiveMind entities.
        /// </summary>
        public static class Query
        {
            /// <summary>
            /// The regex used to parse patterns.
            /// </summary>
            public const string WildcardRegex = "(?<!(?<!\\\\)\\\\)\\*"; 
            /// <summary>
            /// The character to use to define a wildcard in a pattern. Wildcard can be escaped using <see cref="WildcardEscape"/>.
            /// </summary>
            public const char Wildcard = '*';
            /// <summary>
            /// How to escape <see cref="Wildcard"/>.
            /// </summary>
            public const char WildcardEscape = '\\';
            /// <summary>
            /// The maximum amount of results that can be returned from a query.
            /// </summary>
            public const int MaxResultLimit = 10000;
            /// <summary>
            /// The maximum amount of background jobs that can be dequeued in a single call.
            /// </summary>
            public const int MaxDequeueLimit = 1000;
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
            public const string RecurringJobProcessQueueType = "$RecurringJob.Process";
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
                /// <summary>
                /// A flag on a job that indicates a job can be deleted after the configured retention.
                /// </summary>
                public const string MarkedForDeletion = "$MarkedForDeletion";
            }

            /// <summary>
            /// Contains the names of common data set on jobs.
            /// </summary>
            public static class Data
            {
                /// <summary>
                /// The name of the data that contains any defined continuations.
                /// </summary>
                public const string ContinuationsName = "$Continuations";
            }
        }

        /// <summary>
        /// Contains constant/static read only properties related to daemons.
        /// </summary>
        public static class Daemon
        {
            /// <summary>
            /// The name of the property set on daemons to indicate they were auto created.
            /// </summary>
            public const string IsAutoCreatedProperty = "IsAutoCreated";
        }
    }
}
