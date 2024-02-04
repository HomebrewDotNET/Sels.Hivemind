using Microsoft.Extensions.Logging;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.Core.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Async.TaskManagement;
using Microsoft.Extensions.Options;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Creates <see cref="PullthroughScheduler"/> of type <see cref="HiveMindConstants.Scheduling.LazyType"/>.
    /// </summary>
    public class PullthroughSchedulerFactory : IJobSchedulerFactory
    {
        // Fields
        private readonly ILogger _logger;

        // Properties
        /// <inheritdoc/>
        public string Type => PullthroughScheduler.SchedulerType;

        /// <inheritdoc cref="PullthroughSchedulerFactory"/>
        /// <param name="logger">Optional logger for tracing</param>
        public PullthroughSchedulerFactory(ILogger<PullthroughScheduler> logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<IJobScheduler> CreateSchedulerAsync(IServiceProvider serviceProvider, string name, string queueType, IEnumerable<IEnumerable<string>> queueGroups, int levelOfConcurrency, IJobQueue queue, CancellationToken token = default)
        {
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            name.ValidateArgument(nameof(name));
            queueType.ValidateArgument(nameof(queueType));
            queueGroups.ValidateArgumentNotNullOrEmpty(nameof(queueGroups));
            queueGroups.Execute((i, x) => x.ValidateArgumentNotNullOrEmpty($"{nameof(queueGroups)}[{i}]"));
            levelOfConcurrency.ValidateArgumentLargerOrEqual(nameof(levelOfConcurrency), 1);
            queue.ValidateArgument(nameof(queue));

            _logger.Log($"Creating a new Lazy scheduler with name <{name}>");

            var scheduler = new PullthroughScheduler(name, queueType, queueGroups, levelOfConcurrency, queue, serviceProvider.GetRequiredService<ITaskManager>(), serviceProvider.GetRequiredService<IOptionsMonitor<PullthroughSchedulerOptions>>(), serviceProvider.GetService<ILogger<PullthroughScheduler>>());
            _logger.Log($"Created a new Lazy scheduler with name <{name}>");
            return Task.FromResult<IJobScheduler>(scheduler);
        }
    }
}
