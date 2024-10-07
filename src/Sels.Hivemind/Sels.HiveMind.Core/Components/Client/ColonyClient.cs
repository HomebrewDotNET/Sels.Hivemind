using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query;
using Sels.HiveMind.Query.Colony;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Colony;
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
        private readonly IOptionsMonitor<HiveMindOptions> _optionsMonitor;
        private readonly IMemoryCache? _cache;

        /// <inheritdoc cref="ColonyClient"/>
        /// <param name="service">Service used to query colony state</param>
        /// <param name="optionsMonitor">Used to fetch the options for the environment</param>
        /// <param name="cache">Optional memory cache that can be used to speed up conversions</param>
        /// <param name="storageProvider">Service used to get the storage connections</param>
        /// <param name="loggerFactory"><inheritdoc cref="BaseClient._loggerFactory"/></param>
        /// <param name="logger"><inheritdoc cref="BaseClient._logger"/></param>
        public ColonyClient(IColonyService service, IOptionsMonitor<HiveMindOptions> optionsMonitor, IMemoryCache? cache, IStorageProvider storageProvider, ILoggerFactory? loggerFactory = null, ILogger<RecurringJobClient>? logger = null) : base(storageProvider, loggerFactory, logger)
        {
            _service = Guard.IsNotNull(service);
            _optionsMonitor = Guard.IsNotNull(optionsMonitor);
            _cache = cache;
        }
        /// <inheritdoc/>
        public Task<long> CountAsync(IStorageConnection connection, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, CancellationToken token = default)
            => _service.CountAsync(connection, conditionBuilder != null ? new ColonyQueryConditions(conditionBuilder) : new ColonyQueryConditions(), token);
        /// <inheritdoc/>
        public async Task<IClientQueryResult<IColonyInfo>> SearchAsync(IStorageConnection connection, Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>>? conditionBuilder = null, int pageSize = 10000, int page = 1, QueryColonyOrderByTarget orderBy = QueryColonyOrderByTarget.Id, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Querying colonies in environment <{HiveLog.EnvironmentParam}>", connection.Environment);

            var queryConditions = conditionBuilder != null ? new ColonyQueryConditions(conditionBuilder) : new ColonyQueryConditions();

            var results = await _service.SearchAsync(connection, queryConditions, pageSize, page, orderBy, orderByDescending, token).ConfigureAwait(false);

            List<ColonyInfo> colonies = new List<ColonyInfo>();
            var options = _optionsMonitor.Get(connection.Environment);

            foreach (var result in results)
            {
                colonies.Add(new ColonyInfo(result, options, _cache));
            }

            _logger.Log($"Query returned <{results.Length}> colonies");
            return new QueryResult<IColonyInfo>(colonies);
        }
        /// <inheritdoc/>
        public Task<IColonyInfo> GetColonyAsync(IStorageConnection connection, [Traceable("HiveMind.Colony.Id", null)] string id, CancellationToken token = default)
             => _service.GetColonyAsync(connection, id, token);

        /// <inheritdoc/>
        public Task<IColonyInfo?> TryGetColonyAsync(IStorageConnection connection, [Traceable("HiveMind.Colony.Id", null)] string id, CancellationToken token = default)
            => _service.TryGetColonyAsync(connection, id, token);
    }
}
