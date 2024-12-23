﻿using Microsoft.Extensions.Logging;
using Sels.Core;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// The options used by a swarm host.
    /// </summary>
    /// <typeparam name="TOptions">The type of inhereting this interface</typeparam>
    public interface ISwarmHostOptions<TOptions> where TOptions : ISwarmHostOptions<TOptions>
    {
        /// <summary>
        /// The unique name of this (sub) swarm.
        /// </summary>
        public string Name { get;  }
        /// <summary>
        /// How many drones are assigned to this swarm.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public int? Drones { get;  }
        /// <summary>
        /// Optional alias for the the drone. Defaults to Drone.
        /// </summary>
        public string DroneAlias { get; }
        /// <summary>
        /// Factory that will be used to create <see cref="IDroneIdGenerator"/> that will create <see cref="Drones"/> unique id's.
        /// </summary>
        public Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>? DroneIdGeneratorFactory { get; }
        /// <summary>
        /// Sets this swarm as a dedicated swarm. Will only work on queues assigned to this swarm and not to queues assigned to any parent swarms.
        /// </summary>
        public bool IsDedicated { get;  }
        /// <summary>
        /// If this swarm can also work on jobs in the <see cref="HiveMindColonyConstants.Queue.DefaultQueue"/> queue.
        /// When set to null default is true for the root swarm.
        /// </summary>
        public bool? WorkOnGlobalQueue { get;  }
        /// <summary>
        /// The type of scheduler to use for the swarm. 
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public string SchedulerType { get;  }
        /// <summary>
        /// Factory that will be used to create the scheduler for the current swarm. When set to null or when returning null <see cref="SchedulerType"/> will be used.
        /// </summary>
        public Func<IServiceProvider, JobSchedulerConfiguration, Task<IComponent<IJobScheduler>>>? SchedulerFactory { get; }
        /// <summary>
        /// List with the <see cref="IJobSchedulerMiddleware"/>(s) to use for the scheduler. Can be empty.
        /// </summary>
        public IReadOnlyList<ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>>? SchedulerMiddleware { get; }
        /// <summary>
        /// The name to assign to the created scheduler.
        /// The default is the name of the swarm.
        /// </summary>
        public string SchedulerName { get;  }
        /// <summary>
        /// The queues that drones of this swarm can process jobs from.
        /// </summary>
        public IReadOnlyCollection<ISwarmQueue>? Queues { get;  }

        /// <summary>
        /// How long to allow drones to finish their current job before forcefully cancelling the processing.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? GracefulStoptime { get; }
        /// <summary>
        /// The maximum amount of time to wait for drones to stop processing to avoid blocking forever.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? MaxStoptime { get; }
        /// <summary>
        /// How long a drone will sleep when it threw an unhandled exception.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? UnhandledExceptionSleepTime { get; }
        /// <summary>
        /// How long before a lock is supposed to expire to set the heartbeat. Only used for queues that don't manage the locks themselves.
        /// When set to null the default will be used. (Configured globally depending on the job type)
        /// </summary>
        public TimeSpan? LockExpirySafetyOffset { get; }

        /// <summary>
        /// The options of any sub swarms.
        /// </summary>
        public IReadOnlyCollection<TOptions>? SubSwarmOptions { get;  }

        /// <summary>
        /// Gets the name of the swarm and any sub swarms with null, empty and whitespace filtered out.
        /// </summary>
        /// <returns>The names of the swarm and any sub swarms with null, empty and whitespace filtered out</returns>
        public IEnumerable<string> GetDefinedNames()
        {
            if (Name.HasValue()) yield return Name;

            if (SubSwarmOptions.HasValue())
            {
                foreach (var subSwarmOption in SubSwarmOptions!.Where(x => x != null))
                {
                    foreach (var subName in subSwarmOption.GetDefinedNames())
                    {
                        yield return subName;
                    }
                }
            }
        }
    }
}
