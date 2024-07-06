using Castle.DynamicProxy;
using FluentMigrator.Runner;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Polly.Contrib.WaitAndRetry;
using Polly;
using Sels.Core.Data.FluentMigrationTool;
using Sels.Core.Data.MySQL.Models;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.ServiceBuilder.Template;
using Sels.HiveMind;
using Sels.HiveMind.Requests;
using Sels.HiveMind.Storage.MySql.Deployment;
using Sels.HiveMind.Storage.MySql.Deployment.Migrations;
using Sels.SQL.QueryBuilder;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Factory that creates storage clients for MySql based databases.
    /// </summary>
    public class HiveMindMySqlStorageFactory : BaseProxyGenerator<HiveMindMySqlStorage, HiveMindMySqlStorage, HiveMindMySqlStorageFactory>, IComponentFactory<IStorage>
    {
        // Statics
        internal static readonly List<string> DeployedEnvironments = new List<string>();

        // Fields
        private readonly object _lock = new object();
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<HiveMindMySqlStorageOptions> _options;
        private readonly string _connectionString;
        private readonly IMigrationToolFactory _deployerFactory;
        private readonly ProxyGenerator _generator;

        // State
        private HiveMindMySqlStorage _storage;

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        protected override HiveMindMySqlStorageFactory Self => this;

        /// <inheritdoc cref="HiveMindMySqlStorageFactory"/>
        /// <param name="environment">The HiveMind environment to create clients for</param>
        /// <param name="connectionString">The connection string to use to connect to the database</param>
        /// <param name="options">Used to access the options for each environment</param>
        /// <param name="generator">Used to generate job storage proxies</param>
        /// <param name="migrationToolFactory">Tool used to create a migrator for deploying the database schema</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorageFactory(string environment, string connectionString, IOptionsMonitor<HiveMindMySqlStorageOptions> options, ProxyGenerator generator, IMigrationToolFactory migrationToolFactory, ILogger<HiveMindMySqlStorageFactory> logger = null)
        {
            Name = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            var parsedConnectionString = ConnectionString.Parse(connectionString);
            if (!parsedConnectionString.AllowUserVariables)
            {
                parsedConnectionString.AllowUserVariables = true;
                connectionString = parsedConnectionString.ToString();
            }

            _options = options.ValidateArgument(nameof(options));
            _connectionString = connectionString;
            _deployerFactory = migrationToolFactory.ValidateArgument(nameof(migrationToolFactory));
            _logger = logger;
            _generator = generator.ValidateArgument(nameof(generator));

            var currentOptions = _options.Get(Name);

            // Configure proxy
            this.Trace(x => x.Duration.OfAll.WithDurationThresholds(currentOptions.PerformanceWarningThreshold, currentOptions.PerformanceErrorThreshold).And.WithScope.ForAll, true);
            if (currentOptions.MaxRetryCount > 0) this.ExecuteWithPolly((p, b) =>
            {
                var logger = p.GetService<ILogger<HiveMindMySqlStorage>>();
                var transientPolicy = Policy.Handle<MySqlException>(x => x.IsTransient && !(x.ErrorCode == MySqlErrorCode.UnableToConnectToHost && Regex.IsMatch(x.Message, "All pooled connections are in use")))
                                                       .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(currentOptions.MedianFirstRetryDelay, currentOptions.MaxRetryCount, fastFirst: false),
                                                       (e, t, r, c) => logger.Warning($"Ran into recoverable exception while calling method. Current retry count is <{r}/{currentOptions.MaxRetryCount}>", e));

                return b.ForAllAsync.ExecuteWith(transientPolicy);
            });
        }

        /// <inheritdoc/>
        public Task<IStorage> CreateAsync(IServiceProvider serviceProvider, CancellationToken token = default)
        {
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            var options = _options.Get(Name);
            if (options.DeploySchema && !DeployedEnvironments.Contains(Name))
            {
                // Deploy schema if needed
                lock (DeployedEnvironments)
                {
                    if (!DeployedEnvironments.Contains(Name))
                    {
                        _logger.Log($"First time creating storage for environment <{HiveLog.EnvironmentParam}>. Deploying database schema", Name);
                        var deployer = _deployerFactory.Create(true)
                                        .ConfigureRunner(x => x.AddMySql5().WithGlobalConnectionString(_connectionString))
                                        .AddMigrationsFrom<VersionOneBackgroundJob>()
                                        .UseVersionTableMetaData<SchemaVersionTableInfo>(x => new SchemaVersionTableInfo(Name));

                        MigrationState.Environment = Name;
                        MigrationState.DeploymentLockName = options.DeploymentLockName;
                        MigrationState.DeploymentLockTimeout = options.DeploymentLockTimeout;

                        deployer.Deploy();
                        _logger.Log($"Deployed latest schema for environment <{HiveLog.EnvironmentParam}>", Name);
                        DeployedEnvironments.Add(Name);
                    }
                    else
                    {
                        _logger.Debug($"Database schema for environment <{HiveLog.EnvironmentParam}> already deployed", Name);
                    }
                } 
            }

            if (_storage != null) return Task.FromResult<IStorage>(_storage);

            // Create client
            lock (_lock)
            {
                if (_storage != null) return Task.FromResult<IStorage>(_storage);

                _logger.Log($"Creating storage for MySql database in environment <{HiveLog.EnvironmentParam}>", Name);
                var storage = new HiveMindMySqlStorage(serviceProvider.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                                       serviceProvider.GetService<IMemoryCache>(),
                                                       serviceProvider.GetRequiredService<IOptionsMonitor<HiveMindMySqlStorageOptions>>(),
                                                       Name,
                                                       _connectionString,
                                                       serviceProvider.GetRequiredService<ICachedSqlQueryProvider>(),
                                                       serviceProvider.GetService<ILogger<HiveMindMySqlStorage>>());
                _logger.Debug($"Creating storage proxy for MySql database in environment <{HiveLog.EnvironmentParam}>", Name);
                _storage = GenerateProxy(serviceProvider, _generator, storage);
                return Task.FromResult<IStorage>(_storage);
            }            
        }
    }
}
