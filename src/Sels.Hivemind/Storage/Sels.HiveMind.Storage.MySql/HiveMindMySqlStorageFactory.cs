using FluentMigrator.Runner;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Data.FluentMigrationTool;
using Sels.Core.Data.MySQL.Models;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
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

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Factory that creates storage clients for MySql based databases.
    /// </summary>
    public class HiveMindMySqlStorageFactory : IStorageFactory
    {
        // Statics
        internal static readonly List<string> DeployedEnvironments = new List<string>();

        // Fields
        private readonly ILogger _logger;
        private readonly IOptionsSnapshot<HiveMindMySqlStorageOptions> _optionsSnapshot;
        private readonly string _connectionString;
        private readonly IMigrationToolFactory _deployerFactory;
        private readonly bool _isMariaDb;

        // Properties
        /// <inheritdoc/>
        public string Environment { get; }

        /// <inheritdoc cref="HiveMindMySqlStorageFactory"/>
        /// <param name="environment">The HiveMind environment to create clients for</param>
        /// <param name="connectionString">The connection string to use to connect to the database</param>
        /// <param name="optionsSnapshot">Used to access the options for each environment</param>
        /// <param name="isMariaDb">Indicates if the target database is a MariaDb database. Uses slighty different queries</param>
        /// <param name="migrationToolFactory">Tool used to create a migrator for deploying the database schema</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorageFactory(string environment, string connectionString, bool isMariaDb, IOptionsSnapshot<HiveMindMySqlStorageOptions> optionsSnapshot, IMigrationToolFactory migrationToolFactory, ILogger<HiveMindMySqlStorageFactory> logger = null)
        {
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            var parsedConnectionString = ConnectionString.Parse(connectionString);
            if (!parsedConnectionString.AllowUserVariables)
            {
                parsedConnectionString.AllowUserVariables = true;
                connectionString = parsedConnectionString.ToString();
            }

            _optionsSnapshot = optionsSnapshot.ValidateArgument(nameof(optionsSnapshot));
            _isMariaDb = isMariaDb;
            _connectionString = connectionString;
            _deployerFactory = migrationToolFactory.ValidateArgument(nameof(migrationToolFactory));
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<IStorage> CreateStorageAsync(IServiceProvider serviceProvider, CancellationToken token = default)
        {
            serviceProvider.ValidateArgument(nameof(serviceProvider));
            var options = _optionsSnapshot.Get(Environment);
            if (options.DeploySchema)
            {
                // Deploy schema if needed
                lock (DeployedEnvironments)
                {
                    if (!DeployedEnvironments.Contains(Environment))
                    {
                        _logger.Log($"First time creating storage for environment <{Environment}>. Deploying database schema");
                        var deployer = _deployerFactory.Create(true)
                                        .ConfigureRunner(x => x.AddMySql5().WithGlobalConnectionString(_connectionString))
                                        .AddMigrationsFrom<VersionOneBackgroundJob>()
                                        .UseVersionTableMetaData<SchemaVersionTableInfo>(x => new SchemaVersionTableInfo(Environment));

                        MigrationState.Environment = Environment;
                        MigrationState.DeploymentLockName = options.DeploymentLockName;
                        MigrationState.DeploymentLockTimeout = options.DeploymentLockTimeout;

                        deployer.Deploy();
                        _logger.Log($"Deployed latest schema for environment <{Environment}>");
                        DeployedEnvironments.Add(Environment);
                    }
                    else
                    {
                        _logger.Debug($"Database schema for environment <{Environment}> already deployed");
                    }
                } 
            }

            // Create client
            if(_isMariaDb)
            {
                throw new NotImplementedException();
            }
            else
            {
                _logger.Log($"Creating storage for MySql database in environment <{Environment}>");
                return Task.FromResult<IStorage>(new HiveMindMySqlStorage(serviceProvider.GetRequiredService<IOptionsSnapshot<HiveMindOptions>>(),
                                                                          serviceProvider.GetService<IMemoryCache>(),
                                                                          options,
                                                                          Environment,
                                                                          _connectionString,
                                                                          serviceProvider.GetRequiredService<ICachedSqlQueryProvider>(),
                                                                          serviceProvider.GetService<ILogger<HiveMindMySqlStorage>>()));
            }
        }
    }
}
