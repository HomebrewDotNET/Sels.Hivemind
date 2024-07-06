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
        /// For usage inside log message templates.
        /// </summary>
        public const string EnvironmentParam = "{HiveMind.Environment}";
        /// <summary>
        /// Log parameter that contains the name of a HiveMind environment.
        /// </summary>
        public const string Environment = "HiveMind.Environment";

        /// <summary>
        /// Contains the log parameters related (background, recurring, ...) jobs.
        /// </summary>
        public static class Job
        {
            /// <summary>
            /// Log parameter that contains the id of a job.
            /// For usage inside log message templates.
            /// </summary>
            public const string IdParam = "{HiveMind.Job.Id}";
            // <summary>
            /// Log parameter that contains the id of a job.
            /// </summary>
            public const string Id = "HiveMind.Job.Id";
            /// <summary>
            /// Log parameter that contains the type of job.
            /// </summary>
            public const string Type = "HiveMind.Job.Type";
            /// <summary>
            /// Log parameter that contains the current execution id of a jo.
            /// </summary>
            public const string ExecutionId = "HiveMind.Job.ExecutionId";
            /// <summary>
            /// Log parameter that contains the current state name of a job.
            /// For usage inside log message templates.
            /// </summary>
            public const string StateParam = "{HiveMind.Job.State}";
            /// <summary>
            /// Log parameter that contains the current state name of a job.
            /// </summary>
            public const string State = "HiveMind.Job.State";
            /// <summary>
            /// Log parameter that contains the queue of a job.
            /// For usage inside log message templates.
            /// </summary>
            public const string QueueParam = "{HiveMind.Job.Queue}";
            /// <summary>
            /// Log parameter that contains the queue of a job.
            /// </summary>
            public const string Queue = "HiveMind.Job.Queue";
            /// <summary>
            /// Log parameter that contains the queue type of a job queue.
            /// For usage inside log message templates.
            /// </summary>
            public const string QueueTypeParam = "{HiveMind.Job.QueueType}";
            /// <summary>
            /// Log parameter that contains the queue type of a job queue.
            /// </summary>
            public const string QueueType = "HiveMind.Job.QueueType";
            /// <summary>
            /// Log parameter that contains the name of the process that has the active lock on a job.
            /// For usage inside log message templates.
            /// </summary>
            public const string LockHolderParam = "{HiveMind.Job.LockHolder}";
            /// <summary>
            /// Log parameter that contains the name of the process that has the active lock on a job.
            /// </summary>
            public const string LockHolder = "HiveMind.Job.LockHolder";
            /// <summary>
            /// Log parameter that contains the priority of a job.
            /// </summary>
            public const string PriorityParam = "{HiveMind.Job.Priority}";
            /// <summary>
            /// Log parameter that contains the priority of a job.
            /// </summary>
            public const string Priority = "HiveMind.Job.Priority";

            /// <summary>
            /// The type name used for background jobs.
            /// </summary>
            public const string BackgroundJobType = "Background";
            /// <summary>
            /// The type name used for recurring jobs.
            /// </summary>
            public const string RecurringJobType = "Recurring";
        }

        /// <summary>
        /// Contains the log parameters related to distributed locks.
        /// </summary>
        public static class DistributedLocking
        {
            /// <summary>
            /// Log parameter that contains the name of the resource being locked.
            /// </summary>
            public const string Resource = "HiveMind.DistributedLock.Resource";
            /// <summary>
            /// Log parameter that contains the process requesting the lock.
            /// </summary>
            public const string Process = "HiveMind.DistributedLock.Process";
        }

        /// <summary>
        /// Contains the log parameters related background jobs.
        /// </summary>
        public static class BackgroundJob
        {
            
        }

        /// <summary>
        /// Contains the log parameters related to colonies.
        /// </summary>
        public static class Colony
        {
            /// <summary>
            /// Log parameter that contains the name of a colony.
            /// For usage inside log message templates.
            /// </summary>
            public const string NameParam = "{HiveMind.Colony.Name}";
            /// <summary>
            /// Log parameter that contains the name of a colony.
            /// </summary>
            public const string Name = "HiveMind.Colony.Name";
        }

        /// <summary>
        /// Contains the log parameters related to daemons.
        /// </summary>
        public static class Daemon
        {
            /// <summary>
            /// Log parameter that contains the name of a daemon.
            /// For usage inside log message templates.
            /// </summary>
            public const string NameParam = "{HiveMind.Daemon.Name}";
            /// <summary>
            /// Log parameter that contains the name of a daemon.
            /// </summary>
            public const string Name = "HiveMind.Daemon.Name";
        }

        /// <summary>
        /// Contains the log parameters related to worker swarms.
        /// </summary>
        public static class Swarm
        {
            /// <summary>
            /// Log parameter that contains the name of a swarm.
            /// For usage inside log message templates.
            /// </summary>
            public const string NameParam = "{HiveMind.Swarm.Name}";
            /// <summary>
            /// Log parameter that contains the name of a swarm.
            /// </summary>
            public const string Name = "HiveMind.Swarm.Name";
            /// <summary>
            /// Log parameter that contains the name of a drone.
            /// For usage inside log message templates.
            /// </summary>
            public const string DroneNameParam = "{HiveMind.Swarm.Drone.Name}";
            /// <summary>
            /// Log parameter that contains the name of a drone.
            /// </summary>
            public const string DroneName = "HiveMind.Swarm.Drone.Name";
            /// <summary>
            /// Log parameter that contains the full name of a drone.
            /// For usage inside log message templates.
            /// </summary>
            public const string DroneFullNameParam = "{HiveMind.Swarm.Drone.FullName}";
            /// <summary>
            /// Log parameter that contains the full name of a drone.
            /// </summary>
            public const string DroneFullName = "HiveMind.Swarm.Drone.FullName";
            /// <summary>
            /// Log parameter that contains the alias of a drone.
            /// For usage inside log message templates.
            /// </summary>
            public const string DroneAliasParam = "{HiveMind.Swarm.Drone.Alias}";
            /// <summary>
            /// Log parameter that contains the alias of a drone.
            /// </summary>
            public const string DroneAlias = "HiveMind.Swarm.Drone.Alias";
            /// <summary>
            /// Log parameter that contains the id of a drone.
            /// For usage inside log message templates.
            /// </summary>
            public const string DroneIdParam = "{HiveMind.Swarm.Drone.Id}";
            /// <summary>
            /// Log parameter that contains the id of a drone.
            /// </summary>
            public const string DroneId = "HiveMind.Swarm.Drone.Id";
        }
    }
}
