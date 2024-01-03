using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <inheritdoc cref="IColonyFactory"/>
    public class ColonyFactory : IColonyFactory
    {
        // Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        /// <inheritdoc cref="ColonyFactory"/>
        /// <param name="serviceProvider">Service provider used to resolve dependencies for the colonies where the lifetime is the same as the colony</param>
        /// <param name="logger">Optional logger for tracing</param>
        public ColonyFactory(IServiceProvider serviceProvider, ILogger<ColonyFactory> logger = null)
        {
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _logger = logger;
        }

        /// <inheritdoc/>
        public IColony Create(Action<IColonyBuilder> builder)
        {
            builder.ValidateArgument(nameof(builder));
            _logger.Log($"Creating new colony");

            AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var colony = new HiveColony(builder,
                                        scope,
                                        scope.ServiceProvider.GetRequiredService<ITaskManager>(),
                                        scope.ServiceProvider.GetService<IColonyIdentityProvider>(),
                                        scope.ServiceProvider.GetService<ILoggerFactory>(),
                                        scope.ServiceProvider.GetService<ILogger<HiveColony>>());

                _logger.Log($"Created colony <{HiveLog.Colony.Name}> in environment <{HiveLog.Environment}>", colony.Name, colony.Environment);
                return colony;
            }
            catch (Exception)
            {
                scope.Dispose();
                throw;
            }
        }
    }
}
