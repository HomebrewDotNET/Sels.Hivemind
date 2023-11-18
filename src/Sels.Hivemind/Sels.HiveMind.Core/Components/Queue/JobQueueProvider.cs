using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Components;
using Sels.HiveMind.Service.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Queue
{
    /// <inheritdoc cref="IJobQueueProvider"/>
    public class JobQueueProvider : IJobQueueProvider
    {
        // Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IJobQueueFactory> _queueFactories;
        private readonly ILogger _logger;

        /// <inheritdoc cref="JobQueueProvider"/>
        /// <param name="serviceProvider">Used to resolve dependencies</param>
        /// <param name="queueFactories">The registered factories for making queues for interacting with HiveMInd environments</param>
        /// <param name="logger">Optional logger for tracing</param>
        public JobQueueProvider(IServiceProvider serviceProvider, IEnumerable<IJobQueueFactory> queueFactories, ILogger<JobQueueProvider> logger = null)
        {
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _queueFactories = queueFactories.ValidateArgument(nameof(queueFactories));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IEnvironmentComponent<IJobQueue>> GetQueueAsync(string environment, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            _logger.Log($"Creating new job queue for environment <{environment}>");

            var factory = _queueFactories.LastOrDefault(x => environment.Equals(x.Environment, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create job queues for environment <{environment}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var jobQueue = await factory.CreateQueueAsync(_serviceProvider, token).ConfigureAwait(false);
                _logger.Log($"Created new job queue <{jobQueue}> for environment <{environment}>");
                return new ScopedEnvironmentComponent<IJobQueue>(environment, jobQueue, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
