using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Templates.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Client
{
    /// <inheritdoc cref="IColonyClient"/>
    public class ColonyClient : BaseClient, IColonyClient
    {
        // Fields
        private readonly IColonyService _service;

        /// <inheritdoc cref="ColonyClient"/>
        /// <param name="service">Service used to query colony state</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="loggerFactory"><inheritdoc cref="BaseClient._loggerFactory"/></param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public ColonyClient(IColonyService service, IStorageProvider storageProvider, ILoggerFactory? loggerFactory = null, ILogger<RecurringJobClient>? logger = null) : base(storageProvider, loggerFactory, logger)
        {
            _service = Guard.IsNotNull(service);
        }

        /// <inheritdoc/>
        public Task<IColonyInfo> GetColonyAsync(IStorageConnection connection, [Traceable("HiveMind.Colony.Id", null)] string id, CancellationToken token = default)
             => _service.GetColonyAsync(connection, id, token);
        /// <inheritdoc/>
        public Task<IColonyInfo?> TryGetColonyAsync(IStorageConnection connection, [Traceable("HiveMind.Colony.Id", null)] string id, CancellationToken token = default)
            => _service.TryGetColonyAsync(connection, id, token);
    }
}
