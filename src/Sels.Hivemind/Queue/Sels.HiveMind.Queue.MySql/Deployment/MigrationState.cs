using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue.MySql.Deployment
{
    /// <summary>
    /// Used to pass down parameters to migrations.
    /// </summary>
    internal static class MigrationState
    {
        /// <summary>
        /// The environment to deploy to.
        /// </summary>
        public static string Environment { get; set; }
        /// <summary>
        /// The name of the distributed lock that will be used to synchronize deployments.
        /// </summary>
        public static string DeploymentLockName = "Deployment";
        /// <summary>
        /// How long to wait for the deployment lock before throwing an error.
        /// </summary>
        public static TimeSpan DeploymentLockTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Contains the names of various sql objects.
        /// </summary>
        public static class Names
        {
            /// <summary>
            /// The name of the job queue table.
            /// </summary>
            public static string JobQueueTable => $"HiveMind.{Environment}.JobQueue";
            /// <summary>
            /// The name of the queue table that just contains the background jobs to process.
            /// </summary>
            public static string BackgroundJobProcessQueueTable => $"HiveMind.{Environment}.BackgroundJobProcessQueue";
            /// <summary>
            /// The name of the queue table that just contains the background jobs to cleanup.
            /// </summary>
            public static string BackgroundJobCleanupQueueTable => $"HiveMind.{Environment}.BackgroundJobCleanupQueue";
            /// <summary>
            /// The name of the queue table that just contains the recurring jobs to trigger.
            /// </summary>
            public static string RecurringJobTriggerQueueTable => $"HiveMind.{Environment}.RecurringJobTriggerQueue";
        }
    }
}
