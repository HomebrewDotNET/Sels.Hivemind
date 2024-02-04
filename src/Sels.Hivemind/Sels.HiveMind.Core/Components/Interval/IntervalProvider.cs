using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sels.HiveMind.Interval
{
    /// <inheritdoc cref="IIntervalProvider"/>
    public class IntervalProvider : IIntervalProvider
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IIntervalFactory> _intervalFactories;

        /// <inheritdoc cref="IntervalProvider"/>
        /// <param name="serviceProvider">Provider to use to resolve any dependencies for created intervals</param>
        /// <param name="intervalFactories">Any registered interval factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public IntervalProvider(IServiceProvider serviceProvider, IEnumerable<IIntervalFactory> intervalFactories, ILogger<IntervalProvider> logger = null)
        {
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _intervalFactories = intervalFactories.ValidateArgument(nameof(intervalFactories));
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IComponent<IInterval>> GetIntervalAsync(string type, CancellationToken token = default)
        {
            type.ValidateArgumentNotNullOrWhitespace(nameof(type));

            _logger.Log($"Creating new interval of type <{type}>");

            var factory = _intervalFactories.LastOrDefault(x => type.Equals(x.Type, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create intervals of type <{type}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var interval = await factory.CreateIntervalAsync(scope.ServiceProvider, token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Interval factory of type <{factory.Type}> returned null");
                _logger.Log($"Created <{interval}> of type <{type}>");
                return new ScopedComponent<IInterval>(interval, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
