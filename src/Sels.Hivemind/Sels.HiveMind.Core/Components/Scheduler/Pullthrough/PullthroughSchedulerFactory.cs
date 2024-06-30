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
    public class PullthroughSchedulerFactory : IComponentFactory<IJobScheduler, JobSchedulerConfiguration>
    {
        // Fields
        private readonly ILogger _logger;

        // Properties
        /// <inheritdoc/>
        public string Name => PullthroughScheduler.SchedulerType;

        /// <inheritdoc cref="PullthroughSchedulerFactory"/>
        /// <param name="logger">Optional logger for tracing</param>
        public PullthroughSchedulerFactory(ILogger<PullthroughScheduler> logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<IJobScheduler> CreateAsync(IServiceProvider serviceProvider, JobSchedulerConfiguration configuration, CancellationToken token = default)
        {
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            configuration.ValidateArgument(nameof(configuration));

            _logger.Log($"Creating a new Pullthrough scheduler with name <{configuration.Name}>");
            var scheduler = new PullthroughScheduler(serviceProvider.GetRequiredService<IOptionsMonitor<PullthroughSchedulerOptions>>(), configuration.Name, configuration.QueueType, configuration.QueueGroups, configuration.LevelOfConcurrency, configuration.Queue, serviceProvider.GetRequiredService<ITaskManager>(), serviceProvider.GetService<ILogger<PullthroughScheduler>>());
            _logger.Log($"Created a new Pullthrough scheduler with name <{configuration.Name}>");
            return Task.FromResult<IJobScheduler>(scheduler);
        }
    }
}
