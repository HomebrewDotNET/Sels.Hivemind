using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator;
using Sels.HiveMind.Colony.Events;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IColonyFactory"/>
    public class ColonyFactory : IColonyFactory
    {
        // Fields
        private readonly INotifier _notifier;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        /// <inheritdoc cref="ColonyFactory"/>
        /// <param name="notifier">Used to raise events</param>
        /// <param name="serviceProvider">Service provider used to resolve dependencies for the colonies where the lifetime is the same as the colony</param>
        /// <param name="logger">Optional logger for tracing</param>
        public ColonyFactory(INotifier notifier, IServiceProvider serviceProvider, ILogger<ColonyFactory> logger = null)
        {
            _notifier = notifier.ValidateArgument(nameof(_notifier));
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IColony> CreateAsync(Action<IColonyBuilder> builder, CancellationToken token)
        {
            builder.ValidateArgument(nameof(builder));
            _logger.Log($"Creating new colony");

            AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            HiveColony? colony = null;
            try
            {
                colony = new HiveColony(builder,
                                        scope,
                                        scope.ServiceProvider.GetRequiredService<IStorageProvider>(),
                                        scope.ServiceProvider.GetRequiredService<IColonyService>(),
                                        scope.ServiceProvider.GetRequiredService<INotifier>(),
                                        scope.ServiceProvider.GetRequiredService<ITaskManager>(),
                                        scope.ServiceProvider.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                        scope.ServiceProvider.GetService<IColonyIdentityProvider>(),
                                        scope.ServiceProvider.GetService<ILoggerFactory>(),
                                        scope.ServiceProvider.GetService<ILogger<HiveColony>>());

                await _notifier.RaiseEventAsync(this, new ColonyCreatedEvent(colony), token).ConfigureAwait(false);

                _logger.Log($"Created colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", colony.Name, colony.Environment);
                return colony;
            }
            catch (Exception ex)
            {
                _logger.Log($"Could not create colony <{HiveLog.Colony.NameParam}> in environment <{HiveLog.EnvironmentParam}>", ex, colony?.Name, colony?.Environment);

                if(colony != null) await colony.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
