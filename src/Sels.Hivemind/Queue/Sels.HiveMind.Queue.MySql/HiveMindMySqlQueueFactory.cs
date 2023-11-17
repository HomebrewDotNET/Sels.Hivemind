using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.Core.Data.FluentMigrationTool;
using Sels.Core.Data.MySQL.Models;
using Sels.Core.Extensions.Logging;
using FluentMigrator.Runner;
using Sels.HiveMind.Queue.MySql.Deployment.Migrations;
using Sels.HiveMind.Queue.MySql.Deployment;
using Sels.SQL.QueryBuilder;
using Sels.Core.Async.TaskManagement;
using Sels.Core;
using Sels.Core.Extensions.Conversion;

namespace Sels.HiveMind.Queue.MySql
{
    /// <summary>
    /// Manages MySQL based queues.
    /// Responsible for releasing timed out dequeued jobs and keeping them alive while they are active.
    /// Acts as a factory that creates job queues.
    /// </summary>
    public class HiveMindMySqlQueueFactory: IJobQueueFactory, IAsyncDisposable
    {
        // Statics
        internal static readonly List<string> DeployedEnvironments = new List<string>();

        // Fields
        private readonly ILogger _logger;
        private readonly IOptionsSnapshot<HiveMindMySqlQueueOptions> _optionsSnapshot;
        private readonly string _connectionString;
        private readonly IMigrationToolFactory _deployerFactory;
        private readonly bool _isMariaDb;
        private readonly ITaskManager _taskManager;
        private readonly IServiceProvider _serviceProvider;

        // Properties
        /// <inheritdoc/>
        public string Environment { get; }

        /// <inheritdoc cref="HiveMindMySqlQueueFactory"/>
        /// <param name="environment">The HiveMind environment to create clients for</param>
        /// <param name="connectionString">The connection string to use to connect to the database</param>
        /// <param name="optionsSnapshot">Used to access the options for each environment</param>
        /// <param name="isMariaDb">Indicates if the target database is a MariaDb database. Uses slighty different queries</param>
        /// <param name="serviceProvider">Used to resolved queues for management tasks</param>
        /// <param name="taskManager">Used to manage management tasks</param>
        /// <param name="migrationToolFactory">Tool used to create a migrator for deploying the database schema</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlQueueFactory(string environment, string connectionString, bool isMariaDb, IOptionsSnapshot<HiveMindMySqlQueueOptions> optionsSnapshot, IServiceProvider serviceProvider, ITaskManager taskManager, IMigrationToolFactory migrationToolFactory, ILogger<HiveMindMySqlQueueFactory> logger = null)
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
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));

            // Start unlock task
            _taskManager.TryScheduleAction(this, "UnlockTimedOutTask", false, UnlockTimedOutJobs, x => x.WithManagedOptions(ManagedTaskOptions.KeepAlive));
        }

        /// <inheritdoc/>
        public Task<IJobQueue> CreateQueueAsync(IServiceProvider serviceProvider, CancellationToken token = default)
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
                        _logger.Log($"First time creating job queue for environment <{Environment}>. Deploying database schema");
                        var deployer = _deployerFactory.Create(true)
                                        .ConfigureRunner(x => x.AddMySql5().WithGlobalConnectionString(_connectionString))
                                        .AddMigrationsFrom<VersionOneJobQueue>()
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
            if (_isMariaDb)
            {
                throw new NotImplementedException();
            }
            else
            {
                _logger.Log($"Creating job queue for MySql database in environment <{Environment}>");
                return Task.FromResult<IJobQueue>(new HiveMindMySqlQueue(serviceProvider.GetRequiredService<IOptionsSnapshot<HiveMindOptions>>(),
                                                                          options,
                                                                          Environment,
                                                                          _connectionString,
                                                                          serviceProvider.GetRequiredService<ICachedSqlQueryProvider>(),
                                                                          serviceProvider.GetService<ILogger<HiveMindMySqlQueue>>()));
            }
        }

        private async Task UnlockTimedOutJobs(CancellationToken token)
        {
            _logger.Log($"Unlocking timed out dequeued jobs every <{_optionsSnapshot.Value.LockTimeout}>");

            while(!token.IsCancellationRequested)
            {
                try
                {
                    _logger.Debug($"Unlocking timed out dequeued jobs in <{_optionsSnapshot.Value.LockTimeout}>");
                    await Helper.Async.Sleep(_optionsSnapshot.Value.LockTimeout, token);
                    if (token.IsCancellationRequested) break;

                    await using var serviceScope = _serviceProvider.CreateAsyncScope();
                    var queue = (await CreateQueueAsync(serviceScope.ServiceProvider, token).ConfigureAwait(false)).CastTo<HiveMindMySqlQueue>();

                    var unlocked = await queue.UnlockExpiredAsync(token).ConfigureAwait(false);
                    _logger.Log($"Unlocked <{unlocked}> timed out dequeued jobs");
                }
                catch(Exception ex)
                {
                    _logger.Log($"Something went wrong while trying to unlock timed out dequeued jobs", ex);
                }
            }

            _logger.Log($"No longer unlocking timed out dequeued jobs every <{_optionsSnapshot.Value.LockTimeout}> because task was cancelled");
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync() => await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
    }
}
