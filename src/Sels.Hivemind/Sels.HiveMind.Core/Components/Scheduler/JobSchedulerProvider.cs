using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <inheritdoc cref="IJobSchedulerProvider"/>
    public class JobSchedulerProvider : IJobSchedulerProvider
    {
        // Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IJobSchedulerFactory> _schedulerFactories;
        private readonly ILogger _logger;

        /// <inheritdoc cref="JobSchedulerProvider"/>
        /// <param name="serviceProvider">Used to resolve dependencies</param>
        /// <param name="schedulerFactories">The registered factories for making job schedulers</param>
        /// <param name="logger">Optional logger for tracing</param>
        public JobSchedulerProvider(IServiceProvider serviceProvider, IEnumerable<IJobSchedulerFactory> schedulerFactories, ILogger<JobSchedulerProvider> logger = null)
        {
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _schedulerFactories = schedulerFactories.ValidateArgument(nameof(schedulerFactories));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IComponent<IJobScheduler>> CreateSchedulerAsync(string type, string name, string queueType, IEnumerable<IEnumerable<string>> queueGroups, int levelOfConcurrency, IJobQueue queue, CancellationToken token = default)
        {
            type.ValidateArgument(nameof(type));
            name.ValidateArgument(nameof(name));
            queueType.ValidateArgument(nameof(queueType));
            queueGroups.ValidateArgumentNotNullOrEmpty(nameof(queueGroups));
            queueGroups.Execute((i, x) => x.ValidateArgumentNotNullOrEmpty($"{nameof(queueGroups)}[{i}]"));
            levelOfConcurrency.ValidateArgumentLargerOrEqual(nameof(levelOfConcurrency), 1);
            queue.ValidateArgument(nameof(queue));

            _logger.Log($"Creating new scheduler <{name}> of type <{type}>");

            var factory = _schedulerFactories.LastOrDefault(x => type.Equals(x.Type, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create schedulers of type <{type}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var scheduler = await factory.CreateSchedulerAsync(_serviceProvider, name, queueType, queueGroups, levelOfConcurrency, queue, token).ConfigureAwait(false);
                _logger.Log($"Created <{scheduler}> <{name}> of type <{type}>");
                return new ScopedComponent<IJobScheduler>(scheduler, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
