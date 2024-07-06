using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Threading;
using Sels.Core.ServiceBuilder.Template;
using Sels.HiveMind.Queue;
using Sels.SQL.QueryBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.DistributedLocking.MySql
{
    /// <summary>
    /// Creates <see cref="IDistributedLockService"/> that uses a MySQL database to manage the locks.
    /// </summary>
    public class HiveMindMySqlDistributedLockServiceFactory : BaseProxyGenerator<IDistributedLockService, HiveMindMySqlDistributedLockService, HiveMindMySqlDistributedLockServiceFactory>, IComponentFactory<IDistributedLockService>
    {
        // Fields
        private readonly object _lock = new object();
        private readonly ILogger? _logger;
        private readonly string _connectionString; 
        private readonly ProxyGenerator _generator;

        // State
        private HiveMindMySqlDistributedLockService _service;

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        protected override HiveMindMySqlDistributedLockServiceFactory Self => this;

        /// <inheritdoc cref="HiveMindMySqlDistributedLockServiceFactory"/>
        /// <param name="environment">The environment the current factory is registered for</param>
        /// <param name="connectionString">The connection string to use to open connections</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlDistributedLockServiceFactory(string environment, string connectionString, ProxyGenerator proxyGenerator, ILogger<HiveMindMySqlDistributedLockServiceFactory>? logger = null)
        {
            Name = Guard.IsNotNullOrWhitespace(environment, nameof(environment));
            _connectionString = Guard.IsNotNullOrWhitespace(connectionString, nameof(connectionString));
            _generator = Guard.IsNotNull(proxyGenerator);
            _logger = logger;

            this.Trace(x => x.WithScope.ForAll);
        }

        /// <inheritdoc/>
        public Task<IDistributedLockService> CreateAsync(IServiceProvider scope, CancellationToken token = default)
        {
            scope = Guard.IsNotNull(scope);

            if (_service != null) return Task.FromResult<IDistributedLockService>(_service);

            lock (_lock)
            {
                if (_service != null) return Task.FromResult<IDistributedLockService>(_service);

                _logger.Log($"Creating new distributed locking service using MySql database for environment <{HiveLog.EnvironmentParam}>", Name);

                _service = new HiveMindMySqlDistributedLockService(Name, _connectionString, scope.GetRequiredService<ICachedSqlQueryProvider>(), scope.GetRequiredService<ITaskManager>(), scope.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(), scope.GetService<ILogger<HiveMindMySqlDistributedLockService>>());

                _logger.Log($"Created new distributed locking service using MySql database for environment <{HiveLog.EnvironmentParam}>", Name);
                return _service.ToTaskResult<IDistributedLockService>();
            }        
        }
    }
}
