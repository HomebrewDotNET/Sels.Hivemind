using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Deployment
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
            /// The name of the background job table.
            /// </summary>
            public static string BackgroundJobTable => $"HiveMind.{Environment}.BackgroundJob";
            /// <summary>
            /// The name of the background job property table.
            /// </summary>
            public static string BackgroundJobPropertyTable => $"HiveMind.{Environment}.BackgroundJobProperty";

            /// <summary>
            /// The name of the background job state table.
            /// </summary>
            public static string BackgroundJobStateTable => $"HiveMind.{Environment}.BackgroundJobState";
            /// <summary>
            /// The name of the background job state property table.
            /// </summary>
            public static string BackgroundJobStatePropertyTable => $"HiveMind.{Environment}.BackgroundJobStateProperty";

            /// <summary>
            /// The name of the background job log table.
            /// </summary>
            public static string BackgroundJobLogTable => $"HiveMind.{Environment}.BackgroundJobLog";
            /// <summary>
            /// The name of the background job data table.
            /// </summary>
            public static string BackgroundJobDataTable => $"HiveMind.{Environment}.BackgroundJobData";
            /// <summary>
            /// The name of the background job action table.
            /// </summary>
            public static string BackgroundJobActionTable => $"HiveMind.{Environment}.BackgroundJobAction";
        }
    }
}
