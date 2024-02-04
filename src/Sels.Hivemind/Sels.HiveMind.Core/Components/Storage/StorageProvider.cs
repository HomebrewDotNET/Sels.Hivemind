using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage
{
    /// <inheritdoc cref="IStorageProvider"/>
    public class StorageProvider : IStorageProvider
    {
        // Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IStorageFactory> _storageFactories;
        private readonly ILogger _logger;

        /// <inheritdoc cref="StorageProvider"/>
        /// <param name="serviceProvider">Used to resolve dependencies</param>
        /// <param name="storageFactories">The registered factories for making storages for interacting with HiveMInd environments</param>
        /// <param name="logger">Optional logger for tracing</param>
        public StorageProvider(IServiceProvider serviceProvider, IEnumerable<IStorageFactory> storageFactories, ILogger<StorageProvider> logger = null)
        {
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _storageFactories = storageFactories.ValidateArgument(nameof(storageFactories));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IEnvironmentComponent<IStorage>> GetStorageAsync(string environment, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            _logger.Log($"Creating new storage for environment <{HiveLog.Environment}>", environment);

            var factory = _storageFactories.LastOrDefault(x => environment.Equals(x.Environment, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create storages for environment <{environment}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var storage = await factory.CreateStorageAsync(scope.ServiceProvider, token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Storage factory for environment <{factory.Environment}> returned null");
                _logger.Log($"Created new storage <{storage}> for environment <{HiveLog.Environment}>", environment);
                return new ScopedEnvironmentComponent<IStorage>(environment, storage, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
