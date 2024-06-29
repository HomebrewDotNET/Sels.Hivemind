using Sels.Core.Extensions;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Contains the configuration needed to create a <see cref="IJobScheduler"/>.
    /// </summary>
    public class JobSchedulerConfiguration
    {
        /// <summary>
        /// The name of the scheduler, mainly used to get configuration.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The type of queue the scheduler should fetch jobs from
        /// </summary>
        public string QueueType { get; }
        /// <summary>
        /// The queues the scheduler should fetch jobs from. Queues are grouped by priority. The queue groups returned first should get priority over groups returned later.
        /// </summary>
        public IEnumerable<IEnumerable<string>> QueueGroups { get; }
        /// <summary>
        /// The maximum amount of concurrent calls the scheduler should optimise for
        /// </summary>
        public int LevelOfConcurrency { get; }
        /// <summary>
        /// The job queue the scheduler should use to fetch jobs from.
        /// </summary>
        public IJobQueue Queue { get; }

        /// <inheritdoc cref="JobSchedulerConfiguration"/>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="queueType"><inheritdoc cref="QueueType"/></param>
        /// <param name="queueGroups"><inheritdoc cref="QueueGroups"/></param>
        /// <param name="levelOfConcurrency"><inheritdoc cref="LevelOfConcurrency"/></param>
        /// <param name="queue"><inheritdoc cref="Queue"/></param>
        public JobSchedulerConfiguration(string name, string queueType, IEnumerable<IEnumerable<string>> queueGroups, int levelOfConcurrency, IJobQueue queue)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            QueueType = queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            QueueGroups = queueGroups.ValidateArgument(nameof(queueGroups));
            LevelOfConcurrency = levelOfConcurrency.ValidateArgumentLargerOrEqual(nameof(levelOfConcurrency), 1);
            Queue = queue.ValidateArgument(nameof(queue));
        }
    }
}
