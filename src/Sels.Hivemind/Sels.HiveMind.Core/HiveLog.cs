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
        /// Log parameter that contains the name of a HiveMind environment.
        /// </summary>
        public const string Environment = "{HiveMind.Environment}";

        /// <summary>
        /// Contains the log parameters related (background, recurring, ...) jobs.
        /// </summary>
        public static class Job
        {
            /// <summary>
            /// Log parameter that contains the id of a background job.
            /// </summary>
            public const string Id = "{HiveMind.Job.Id}";
            /// <summary>
            /// Log parameter that contains the queue of a background job.
            /// </summary>
            public const string Queue = "{HiveMind.Job.Queue}";
            /// <summary>
            /// Log parameter that contains the queue type of a background job queue.
            /// </summary>
            public const string QueueType = "{HiveMind.Job.QueueType}";
            /// <summary>
            /// Log parameter that contains the name of the process that has the active lock on a job.
            /// </summary>
            public const string LockHolder = "{HiveMind.Job.LockHolder}";
            /// <summary>
            /// Log parameter that contains the priority of a background job.
            /// </summary>
            public const string Priority = "{HiveMind.Job.Priority}";
        }

        /// <summary>
        /// Contains the log parameters related background jobs.
        /// </summary>
        public static class BackgroundJob
        {
            /// <summary>
            /// Log parameter that contains the current state name of a background job.
            /// </summary>
            public const string State = "{HiveMind.BackgroundJob.State}";
        }

        /// <summary>
        /// Contains the log parameters related to colonies.
        /// </summary>
        public static class Colony
        {
            /// <summary>
            /// Log parameter that contains the name of a colony.
            /// </summary>
            public const string Name = "{HiveMind.Colony.Name}";
        }

        /// <summary>
        /// Contains the log parameters related to daemons.
        /// </summary>
        public static class Daemon
        {
            /// <summary>
            /// Log parameter that contains the name of a daemon.
            /// </summary>
            public const string Name = "{HiveMind.Daemon.Name}";
        }

        /// <summary>
        /// Contains the log parameters related to worker swarms.
        /// </summary>
        public static class Swarm
        {
            /// <summary>
            /// Log parameter that contains the name of a swarm.
            /// </summary>
            public const string Name = "{HiveMind.Swarm.Name}";
            public const string DroneName = "{HiveMind.Swarm.DroneName}";
        }
    }
}
