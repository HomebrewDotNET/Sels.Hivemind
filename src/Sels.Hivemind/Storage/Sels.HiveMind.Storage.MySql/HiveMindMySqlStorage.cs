using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.SQL.QueryBuilder;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Sels.Core.Extensions.Linq;
using Sels.SQL.QueryBuilder.Builder;
using Sels.Core.Extensions.Text;
using Sels.Core.Models;
using Sels.SQL.QueryBuilder.Expressions;
using Sels.Core.Conversion.Extensions;
using System.Data;
using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Query.Job;
using Sels.SQL.QueryBuilder.Builder.Statement;
using Sels.HiveMind.Query;
using Sels.HiveMind.Storage.Sql.Templates;
using Sels.Core;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Scope;
using Sels.HiveMind.Storage.Sql;
using Sels.HiveMind.Storage.Sql.Job.Background;
using Sels.SQL.QueryBuilder.Builder.Expressions;
using Sels.HiveMind.Storage.Sql.Job.Recurring;
using Sels.Core.Extensions.Reflection;
using Sels.SQL.QueryBuilder.MySQL.MariaDb;
using Sels.HiveMind.Storage.Job.Background;
using Sels.HiveMind.Storage.Job.Recurring;
using StaticSql = Sels.SQL.QueryBuilder.Sql;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Storage.Colony;
using Sels.HiveMind.Storage.Sql.Colony;
using Sels.Core.Extensions.Fluent;
using Sels.HiveMind.Storage.Sql.Models.Colony;
using Sels.Core.Extensions.Collections;
using Sels.Core.Tracing;
using Sels.HiveMind.Query.Colony;
using Sels.Core.Extensions.Equality;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Persists HiveMind state in a MySql database.
    /// </summary>
    public class HiveMindMySqlStorage : IStorage
    {
        // Constants
        private const LogLevel _queryGenerationTraceLevel = LogLevel.Trace;

        // Fields
        private readonly IOptionsMonitor<HiveMindOptions> _hiveOptions;
        private readonly IMemoryCache _cache;
        private readonly IOptionsMonitor<HiveMindMySqlStorageOptions> _options;
        private readonly ICachedSqlQueryProvider _queryProvider;
        private readonly string _environment;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        private readonly ExpressionCompileOptions _compileOptions = ExpressionCompileOptions.AppendSeparator;

        // Properties
        /// <summary>
        /// Contains the table names for the current environment.
        /// </summary>
        protected StorageTableNames TableNames { get; }

        /// <summary>
        /// The configured options for the current environment.
        /// </summary>
        protected HiveMindMySqlStorageOptions Options => _options.Get(_environment);

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="hiveMindOptions">The global hive mind options for this instance</param>
        /// <param name="cache">Optional cache used for type conversion</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptionsMonitor<HiveMindOptions> hiveMindOptions, IMemoryCache cache, IOptionsMonitor<HiveMindMySqlStorageOptions> options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger logger = null)
        {
            _hiveOptions = hiveMindOptions.ValidateArgument(nameof(hiveMindOptions));
            _cache = cache;
            _options = options.ValidateArgument(nameof(options));
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            _connectionString = connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            _queryProvider = queryProvider.ValidateArgument(nameof(queryProvider)).CreateSubCachedProvider(x => x.WithExpressionCompileOptions(_compileOptions).OnBuilderCreated(x =>
            {
                // Set global aliases for all known tables
                if (x is IAliasQueryBuilder aliasBuilder)
                {
                    aliasBuilder.SetAlias<BackgroundJobTable>("B");
                    aliasBuilder.SetAlias<BackgroundJobPropertyTable>("BP");
                    aliasBuilder.SetAlias<BackgroundJobStateTable>("BS");
                    aliasBuilder.SetAlias<BackgroundJobStatePropertyTable>("BSP");
                    aliasBuilder.SetAlias<BackgroundJobActionTable>("BA");
                    aliasBuilder.SetAlias<BackgroundJobDataTable>("BD");
                    aliasBuilder.SetAlias<BackgroundJobLogTable>("BL");

                    aliasBuilder.SetAlias<RecurringJobTable>("R");
                    aliasBuilder.SetAlias<RecurringJobPropertyTable>("RP");
                    aliasBuilder.SetAlias<RecurringJobActionTable>("RA");
                    aliasBuilder.SetAlias<RecurringJobDataTable>("RD");
                    aliasBuilder.SetAlias<RecurringJobLogTable>("RL");
                    aliasBuilder.SetAlias<RecurringJobStateTable>("RS");
                    aliasBuilder.SetAlias<RecurringJobStatePropertyTable>("RSP");


                    aliasBuilder.SetAlias<ColonyTable>("C");
                    aliasBuilder.SetAlias<ColonyPropertyTable>("CP");
                    aliasBuilder.SetAlias<ColonyDaemonTable>("CD");
                    aliasBuilder.SetAlias<ColonyDaemonPropertyTable>("CDP");
                    aliasBuilder.SetAlias<ColonyDaemonLogTable>("CDL");
                }

                // Rename tables to their actual names
                x.OnCompiling(x =>
                {
                    if (TableNames != null && x is TableExpression tableExpression && tableExpression.Alias?.Set != null)
                    {
                        switch (tableExpression.Alias.Set)
                        {
                            case Type t when t.Is<BackgroundJobTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobTable);
                                break;
                            case Type t when t.Is<BackgroundJobPropertyTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobPropertyTable);
                                break;
                            case Type t when t.Is<BackgroundJobStateTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobStateTable);
                                break;
                            case Type t when t.Is<BackgroundJobStatePropertyTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobStatePropertyTable);
                                break;
                            case Type t when t.Is<BackgroundJobActionTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobActionTable);
                                break;
                            case Type t when t.Is<BackgroundJobDataTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobDataTable);
                                break;
                            case Type t when t.Is<BackgroundJobLogTable>():
                                tableExpression.SetTableName(TableNames.BackgroundJobLogTable);
                                break;
                            case Type t when t.Is<RecurringJobTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobTable);
                                break;
                            case Type t when t.Is<RecurringJobPropertyTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobPropertyTable);
                                break;
                            case Type t when t.Is<RecurringJobActionTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobActionTable);
                                break;
                            case Type t when t.Is<RecurringJobDataTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobDataTable);
                                break;
                            case Type t when t.Is<RecurringJobLogTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobLogTable);
                                break;
                            case Type t when t.Is<RecurringJobStateTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobStateTable);
                                break;
                            case Type t when t.Is<RecurringJobStatePropertyTable>():
                                tableExpression.SetTableName(TableNames.RecurringJobStatePropertyTable);
                                break;
                            case Type t when t.Is<ColonyTable>():
                                tableExpression.SetTableName(TableNames.ColonyTable);
                                break;
                            case Type t when t.Is<ColonyPropertyTable>():
                                tableExpression.SetTableName(TableNames.ColonyPropertyTable);
                                break;
                            case Type t when t.Is<ColonyDaemonTable>():
                                tableExpression.SetTableName(TableNames.ColonyDaemonTable);
                                break;
                            case Type t when t.Is<ColonyDaemonPropertyTable>():
                                tableExpression.SetTableName(TableNames.ColonyDaemonPropertyTable);
                                break;
                            case Type t when t.Is<ColonyDaemonLogTable>():
                                tableExpression.SetTableName(TableNames.ColonyDaemonLogTable);
                                break;
                        }
                    }
                });
            }));
            _logger = logger;
            TableNames = new StorageTableNames(_environment);
        }

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="options">The options for this instance</param>
        /// <param name="cache">Optional cache used for type conversion</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptionsMonitor<HiveMindOptions> hiveMindOptions, IMemoryCache cache, IOptionsMonitor<HiveMindMySqlStorageOptions> options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger<HiveMindMySqlStorage> logger = null) : this(hiveMindOptions, cache, options, environment, connectionString, queryProvider, logger.CastToOrDefault<ILogger>())
        {
        }

        /// <summary>
        /// Proxy generator.
        /// </summary>
        protected HiveMindMySqlStorage()
        {
        }

        /// <inheritdoc/>
        public virtual async Task<IStorageConnection> OpenConnectionAsync(bool startTransaction, CancellationToken token = default)
        {
            _logger.Log($"Opening new connection to MySql storage in environment <{HiveLog.EnvironmentParam}>", _environment);

            var connection = new MySqlConnection(_connectionString);
            MySqlStorageConnection storageConnection = null;
            try
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                storageConnection = new MySqlStorageConnection(connection, this, _environment);
                if (startTransaction) await storageConnection.BeginTransactionAsync(token).ConfigureAwait(false);
                return storageConnection;
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                // Close connection if it exists
                if (storageConnection != null)
                {
                    try
                    {
                        await storageConnection.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }

                if (exceptions.HasValue())
                {
                    exceptions.Insert(0, ex);
                    throw new AggregateException($"Could not open connection to MySql storage in environment <{_environment}>", exceptions);
                }
                throw;
            }
        }
        /// <summary>
        /// Parses <paramref name="connection"/> as <see cref="MySqlStorageConnection"/>.
        /// </summary>
        /// <param name="connection">The connection to parse</param>
        /// <returns>The connection parsed from <paramref name="connection"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        protected MySqlStorageConnection GetStorageConnection(IStorageConnection connection, bool requiresTransaction = false)
        {
            connection.ValidateArgument(nameof(connection));

            if (connection is MySqlStorageConnection storageConnection)
            {
                if (!storageConnection.Environment.EqualsNoCase(_environment)) throw new InvalidOperationException($"Storage connection was opened for environment <{storageConnection.Environment}> but storage is configured for <{_environment}>");
                if (requiresTransaction && !storageConnection.HasTransaction) throw new InvalidOperationException($"Action requires transaction to be opened");

                return storageConnection;
            }

            throw new InvalidOperationException($"Expected connection to be of type <{typeof(MySqlStorageConnection)}> but got <{connection}>");
        }

        #region BackgroundJob
        /// <inheritdoc/>
        public virtual Task<IAsyncDisposable> AcquireDistributedLockForBackgroundJobAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection);

            var jobId = id.ConvertTo<long>();

            _logger.Log($"Acquiring distributed lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            var lockName = $"HiveMind.{connection.Environment}.BackgroundJob.{jobId}";
            var timeout = Options.DistributedLockTimeout;
            var timeoutMessage = $"Could not acquire a distributed lock on background job <{jobId}> in environment <{connection.Environment}> in <{timeout}>";

            var lockQuery = _queryProvider.If().Condition(x => x.GetLock(lockName, timeout.TotalSeconds).NotEqualTo.Value(1))
                                               .Then(x => x.Signal(timeoutMessage), false)
                                          .Build(_compileOptions);
            var unlockQuery = _queryProvider.Select().ColumnExpression(x => x.ReleaseLock(lockName))
                                            .Build(_compileOptions);

            var lockAction = new AsyncScopedAction(async () =>
            {
                _logger.Trace($"Acquiring distributed lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{lockQuery}>", jobId, connection.Environment);
                _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(lockQuery, transaction: storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Log($"Acquired distributed lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", jobId, connection.Environment);
            }, async () =>
            {
                _logger.Trace($"Releasing distributed lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{unlockQuery}>", jobId, connection.Environment);
                _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(unlockQuery, transaction: storageConnection.MySqlTransaction)).ConfigureAwait(false);
                _logger.Log($"Released distributed lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", jobId, connection.Environment);
            });

            return lockAction.StartAsync();
        }
        /// <inheritdoc/>
        public virtual async Task<string> CreateBackgroundJobAsync(BackgroundJobStorageData jobData, IStorageConnection connection, CancellationToken token = default)
        {
            //using var actionLogger = _logger.TraceAction(LogLevel.Error, nameof(CreateBackgroundJobAsync));
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection, true);

            var stateAmount = jobData.ChangeTracker.NewStates?.Count ?? 0;
            var propertyAmount = jobData.ChangeTracker.NewProperties?.Count ?? 0;

            _logger.Log($"Inserting new background job in environment <{HiveLog.EnvironmentParam}> with <{stateAmount}> new states and <{propertyAmount}> new properties", _environment);
            // Job
            var job = new BackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(CreateBackgroundJobAsync)}.{stateAmount}.{propertyAmount}"), x =>
            {
                var builder = x.New();
                var insert = x.Insert<BackgroundJobTable>().Into(table: TableNames.BackgroundJobTable)
                              .Columns(x => x.ExecutionId, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.CreatedAt, x => x.ModifiedAt)
                              .Parameters(x => x.ExecutionId, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.CreatedAt, x => x.ModifiedAt);
                var selectId = x.Select().ColumnExpression(x => x.AssignVariable(nameof(jobData.Id), v => v.LastInsertedId()));
                builder.Append(insert).Append(selectId);

                if (stateAmount > 0)
                {
                    var insertStates = x.Insert<BackgroundJobStateTable>().Into(table: TableNames.BackgroundJobStateTable)
                                        .Columns(c => c.Name, c => c.Sequence, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.Data, c => c.IsCurrent);

                    Enumerable.Range(0, stateAmount).Execute(i =>
                    {
                        insertStates.Values(b => b.Parameter(c => c.Name, $"State{i}"),
                                            b => b.Parameter(c => c.Sequence, $"State{i}"),
                                            b => b.Parameter(c => c.OriginalType, $"State{i}"),
                                            b => b.Variable(nameof(jobData.Id)),
                                            b => b.Parameter(c => c.ElectedDate, $"State{i}"),
                                            b => b.Parameter(c => c.Reason, $"State{i}"),
                                            b => b.Parameter(c => c.Data, $"State{i}"),
                                            b => b.Parameter(c => c.IsCurrent, $"State{i}"));
                    });
                    builder.Append(insertStates);
                }

                if (propertyAmount > 0)
                {
                    var insertProperties = x.Insert<BackgroundJobPropertyTable>().Into(table: TableNames.BackgroundJobPropertyTable)
                                            .Columns(c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt);
                    Enumerable.Range(0, propertyAmount).Execute(i => insertProperties.Values(b => b.Variable(nameof(jobData.Id)),
                                                                                                  b => b.Parameter(c => c.Name, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.Type, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.OriginalType, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.TextValue, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.NumberValue, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.FloatingNumberValue, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.DateValue, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.BooleanValue, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.OtherValue, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.CreatedAt, $"Property{i}"),
                                                                                                  b => b.Parameter(c => c.ModifiedAt, $"Property{i}")));
                    builder.Append(insertProperties);
                }



                return builder.Append(x.Select().Variable(nameof(jobData.Id)));
            });
            var backgroundJobTable = new BackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            var parameters = backgroundJobTable.ToCreateParameters();
            if (stateAmount > 0)
            {
                var states = jobData.ChangeTracker.NewStates.Select(x => new BackgroundJobStateTable(x)).ToArray();
                states.Last().IsCurrent = true;
                states.Execute((i, x) => x.AppendCreateParameters(parameters, $"State{i}"));
            }
            if (propertyAmount > 0) jobData.ChangeTracker.NewProperties.Select(x => new BackgroundJobPropertyTable(x)).Execute((i, x) => x.AppendCreateParameters(parameters, $"Property{i}"));
            _logger.Trace($"Inserting new background job in environment <{HiveLog.EnvironmentParam}> with <{stateAmount}> new states and <{propertyAmount}> new properties using query <{query}>", _environment);

            var id = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Inserted background job <{id}> in environment <{HiveLog.EnvironmentParam}> with <{stateAmount}> new states and <{propertyAmount}> new properties", _environment);
            return id.ToString();
        }
        /// <inheritdoc/>
        public virtual async Task<bool> TryUpdateBackgroundJobAsync(BackgroundJobStorageData jobData, IStorageConnection connection, bool releaseLock, CancellationToken token = default)
        {
            //using var actionLogger = _logger.TraceAction(LogLevel.Error, nameof(TryUpdateBackgroundJobAsync));
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection, true);

            var stateAmount = jobData.ChangeTracker.NewStates?.Count ?? 0;
            var propertyAmount = jobData.ChangeTracker.NewProperties?.Count ?? 0;

            _logger.Log($"Updating background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> with <{stateAmount}> new states and <{propertyAmount}> new properties", jobData.Id, _environment);
            var holder = jobData.Lock.LockedBy;
            var backgroundJobTable = new BackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TryUpdateBackgroundJobAsync)}.{stateAmount}.{propertyAmount}"), x =>
            {
                var builder = x.New();
                var update = x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                                .Set.Column(c => c.Queue).To.Parameter(c => c.Queue)
                                .Set.Column(c => c.Priority).To.Parameter(c => c.Priority)
                                .Set.Column(c => c.ExecutionId).To.Parameter(c => c.ExecutionId)
                                .Set.Column(c => c.ModifiedAt).To.Parameter(c => c.ModifiedAt)
                                .Set.Column(c => c.LockedBy).To.Parameter(c => c.LockedBy)
                                .Set.Column(c => c.LockedAt).To.Parameter(c => c.LockedAt)
                                .Set.Column(c => c.LockHeartbeat).To.Parameter(c => c.LockHeartbeat)
                                .Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id)
                                             .And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
                builder.Append(update);

                var ifUpdatedBuilder = x.If().Condition(x => x.RowCount().GreaterThan.Value(0));
                IIfBodyStatementBuilder bodyBuilder = ifUpdatedBuilder;
                IIfFullStatementBuilder ifUpdated = null;

                ifUpdated = bodyBuilder.Then(x.Select().Value(1));
                bodyBuilder = ifUpdated;

                if (stateAmount > 0 || propertyAmount > 0)
                {
                    if (stateAmount > 0)
                    {
                        var resetStates = x.Update<BackgroundJobStateTable>().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable))
                                            .Set.Column(c => c.IsCurrent).To.Value(false)
                                            .Where(w => w.Column(c => c.BackgroundJobId).EqualTo.Parameter<BackgroundJobTable>(c => c.Id));
                        var insertStates = x.Insert<BackgroundJobStateTable>().Into(table: TableNames.BackgroundJobStateTable)
                                            .Columns(c => c.Name, c => c.Sequence, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.Data, c => c.IsCurrent);
                        Enumerable.Range(0, stateAmount).Execute(i =>
                        {
                            insertStates.Parameters($"State{i}", c => c.Name, c => c.Sequence, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.Data, c => c.IsCurrent);
                        });

                        ifUpdated = bodyBuilder.Then(resetStates).Then(insertStates);
                        bodyBuilder = ifUpdated;
                    }

                    if (propertyAmount > 0)
                    {
                        var insertProperties = x.Insert<BackgroundJobPropertyTable>().Into(table: TableNames.BackgroundJobPropertyTable)
                                                .Columns(c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt);
                        Enumerable.Range(0, propertyAmount).Execute(i => insertProperties.Parameters($"Property{i}", c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt));
                        ifUpdated = bodyBuilder.Then(insertProperties);
                        bodyBuilder = ifUpdated;
                    }
                }
                ifUpdated.Else.Then(x.Select().Value(0));

                builder.Append(ifUpdated);

                return builder;
            });
            _logger.Trace($"Updating background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> with <{stateAmount}> new states and <{propertyAmount}> new properties using query <{query}>", jobData.Id, _environment);

            // Execute query
            var parameters = backgroundJobTable.ToUpdateParameters(holder, releaseLock);
            parameters.AddLocker(holder, nameof(holder));
            if (stateAmount > 0)
            {
                var states = jobData.ChangeTracker.NewStates.Select(x => new BackgroundJobStateTable(x)).ToArray();
                states.Last().IsCurrent = true;
                states.Execute((i, x) =>
                {
                    x.BackgroundJobId = backgroundJobTable.Id;
                    x.AppendCreateParameters(parameters, $"State{i}");
                });
            }
            if (propertyAmount > 0) jobData.ChangeTracker.NewProperties.Select(x => new BackgroundJobPropertyTable(x)).Execute((i, x) =>
            {
                x.BackgroundJobId = backgroundJobTable.Id;
                x.AppendCreateParameters(parameters, $"Property{i}");
            });

            var updated = await storageConnection.MySqlConnection.ExecuteScalarAsync<bool>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (!updated)
            {
                _logger.Warning($"Could not update background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", jobData.Id, _environment);
                return false;
            }
            else
            {
                // Persist changes to properties
                if (jobData.ChangeTracker.UpdatedProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.UpdatedProperties.Select(x => new BackgroundJobPropertyTable(x)).ToArray();
                    await UpdatePropertiesAsync(storageConnection, backgroundJobTable.Id, properties, token).ConfigureAwait(false);
                }
                if (jobData.ChangeTracker.RemovedProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.RemovedProperties.ToArray();
                    await DeletePropertiesAsync(storageConnection, backgroundJobTable.Id, properties, token).ConfigureAwait(false);
                }

                return true;
            }
        }
        /// <summary>
        /// Inserts the new states for background job <paramref name="backgroundJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="backgroundJobId">The id of the background job to insert the state for</param>
        /// <param name="states">The states and their properties to insert</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task InsertStatesWithPropertiesAsync(MySqlStorageConnection connection, long backgroundJobId, (BackgroundJobStateTable State, BackgroundJobStatePropertyTable[] Properties)[] states, CancellationToken token = default)
        {
            //using var actionLogger = _logger.TraceAction(LogLevel.Error, nameof(InsertStatesWithPropertiesAsync));
            using var methodLogger = _logger.TraceMethod(this);
            _logger.Log($"Inserting <{states.Length}> new states for background job <{HiveLog.Job.IdParam}>", backgroundJobId);

            var parameters = new DynamicParameters();

            // Reset is current on existing states
            var resetQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(InsertStatesWithPropertiesAsync)}.Reset"), x =>
            {
                return x.Update<BackgroundJobStateTable>().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable))
                        .Set.Column(c => c.IsCurrent).To.Value(false)
                        .Where(w => w.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)))
                        .Build(_compileOptions);
            });

            parameters.AddBackgroundJobId(backgroundJobId);
            _logger.Trace($"Resetting {nameof(BackgroundJobStateTable.IsCurrent)} to false for existing states for background job <{HiveLog.Job.IdParam}> using query <{resetQuery}>", backgroundJobId);
            await connection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(resetQuery, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            // Insert new
            states.Last().State.IsCurrent = true;
            foreach (var (state, properties) in states)
            {
                state.BackgroundJobId = backgroundJobId;
                state.CreatedAt = DateTime.UtcNow;
                state.ModifiedAt = DateTime.UtcNow;

                var propertyCount = properties?.Length ?? 0;

                // Insert state with it's properties
                _logger.Debug($"Inserting state <{HiveLog.Job.StateParam}> for background job <{HiveLog.Job.IdParam}> with <{propertyCount}> properties", state.Name, backgroundJobId);
                parameters = state.ToCreateParameters();
                var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(InsertStatesWithPropertiesAsync)}.Insert.{propertyCount}"), x =>
                {
                    var insert = x.Insert<BackgroundJobStateTable>().Into(table: TableNames.BackgroundJobStateTable)
                                  .Columns(c => c.Name, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.IsCurrent, c => c.CreatedAt)
                                  .Parameters(c => c.Name, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.IsCurrent, c => c.CreatedAt);

                    var select = x.Select().LastInsertedId();

                    var query = x.New().Append(insert).Append(select);
                    if (propertyCount > 0)
                    {
                        var insertProperties = x.Insert<BackgroundJobStatePropertyTable>().Into(table: TableNames.BackgroundJobStatePropertyTable)
                                            .Columns(c => c.StateId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue);
                        Enumerable.Range(0, propertyCount).Execute(i =>
                        {
                            insertProperties.Values(x => x.LastInsertedId(),
                                                    x => x.Parameter(x => x.Name, i.ToString()),
                                                    x => x.Parameter(x => x.Type, i.ToString()),
                                                    x => x.Parameter(x => x.OriginalType, i.ToString()),
                                                    x => x.Parameter(x => x.TextValue, i.ToString()),
                                                    x => x.Parameter(x => x.NumberValue, i.ToString()),
                                                    x => x.Parameter(x => x.FloatingNumberValue, i.ToString()),
                                                    x => x.Parameter(x => x.DateValue, i.ToString()),
                                                    x => x.Parameter(x => x.BooleanValue, i.ToString()),
                                                    x => x.Parameter(x => x.OtherValue, i.ToString()));
                        });

                        query.Append(insertProperties);
                    }

                    return query;
                });
                properties.Execute((i, x) => x.AppendCreateParameters(parameters, i.ToString()));
                _logger.Trace($"Inserting state <{HiveLog.Job.StateParam}> for background job <{HiveLog.Job.IdParam}> with <{propertyCount}> properties using query <{query}>", state.Name, backgroundJobId);

                state.Id = await connection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Debug($"Inserted state <{HiveLog.Job.StateParam}> for background job <{HiveLog.Job.IdParam}> with id <{state.Id}> with <{propertyCount}> properties", state.Name, backgroundJobId);
                properties.Execute(x => x.StateId = state.Id);
            }

            _logger.Log($"Inserted <{states.Length}> new states for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
        }
        /// <summary>
        /// Inserts properties for background job <paramref name="backgroundJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="backgroundJobId">The id of the job to insert the properties for</param>
        /// <param name="properties">The properties to insert</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task InsertPropertiesAsync(MySqlStorageConnection connection, long backgroundJobId, BackgroundJobPropertyTable[] properties, CancellationToken token = default)
        {
            //using var actionLogger = _logger.TraceAction(LogLevel.Error, nameof(InsertPropertiesAsync));
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Inserting <{properties.Length}> new properties for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
            properties.Execute(x =>
            {
                x.BackgroundJobId = backgroundJobId;
                x.CreatedAt = DateTime.UtcNow;
                x.ModifiedAt = DateTime.UtcNow;
            });

            var parameters = new DynamicParameters();
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(InsertPropertiesAsync)}.{properties.Length}"), x =>
            {
                var insertQuery = x.Insert<BackgroundJobPropertyTable>().Into(table: TableNames.BackgroundJobPropertyTable)
                                   .Columns(c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt);
                Enumerable.Range(0, properties.Length).Execute(x => insertQuery.Parameters(x.ToString(), c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt));
                return insertQuery;
            });
            _logger.Trace($"Inserting <{properties.Length}> properties for background job <{HiveLog.Job.IdParam}> using query <{query}>", backgroundJobId);

            properties.Execute((i, x) => x.AppendCreateParameters(parameters, i.ToString()));
            var inserted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            if (inserted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be inserted but only <{inserted}> were inserted");
            _logger.Log($"Inserted <{inserted}> new properties for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
        }
        /// <summary>
        /// Updates properties for background job <paramref name="backgroundJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="backgroundJobId">The id of the job to insert the properties for</param>
        /// <param name="properties">The properties to update</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task UpdatePropertiesAsync(MySqlStorageConnection connection, long backgroundJobId, BackgroundJobPropertyTable[] properties, CancellationToken token = default)
        {
            //using var actionLogger = _logger.TraceAction(LogLevel.Error, nameof(UpdatePropertiesAsync));
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Updating <{properties.Length}> properties for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
            properties.Execute(x =>
            {
                x.ModifiedAt = DateTime.UtcNow;
            });

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UpdatePropertiesAsync)), x =>
            {
                return x.Update<BackgroundJobPropertyTable>().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable))
                        .Set.Column(c => c.Type).To.Parameter(c => c.Type)
                        .Set.Column(c => c.OriginalType).To.Parameter(c => c.OriginalType)
                        .Set.Column(c => c.TextValue).To.Parameter(c => c.TextValue)
                        .Set.Column(c => c.NumberValue).To.Parameter(c => c.NumberValue)
                        .Set.Column(c => c.FloatingNumberValue).To.Parameter(c => c.FloatingNumberValue)
                        .Set.Column(c => c.DateValue).To.Parameter(c => c.DateValue)
                        .Set.Column(c => c.BooleanValue).To.Parameter(c => c.BooleanValue)
                        .Set.Column(c => c.OtherValue).To.Parameter(c => c.OtherValue)
                        .Set.Column(c => c.ModifiedAt).To.Parameter(c => c.ModifiedAt)
                        .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId))
                                    .And.Column(c => c.Name).EqualTo.Parameter(c => c.Name));
            });
            _logger.Trace($"Updating each property for background job <{HiveLog.Job.IdParam}> using query <{query}>", backgroundJobId);

            foreach (var property in properties)
            {
                _logger.Debug($"Updating property <{property.Name}> for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
                var parameters = property.ToUpdateParameters(null);
                parameters.AddBackgroundJobId(backgroundJobId, nameof(backgroundJobId));

                var updated = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

                if (updated != 1) throw new InvalidOperationException($"Property <{property.Name}> for background job <{backgroundJobId}> was not updated");
                _logger.Debug($"Updated property <{property.Name}> for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
            }

            _logger.Log($"Updated <{properties.Length}> properties for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
        }
        /// <summary>
        /// Deletes properties for background job <paramref name="backgroundJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="backgroundJobId">The id of the job to insert the properties for</param>
        /// <param name="properties">The names of the properties to delete</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task DeletePropertiesAsync(MySqlStorageConnection connection, long backgroundJobId, string[] properties, CancellationToken token = default)
        {
            //using var actionLogger = _logger.TraceAction(LogLevel.Error, nameof(DeletePropertiesAsync));
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Deleting <{properties.Length}> properties for background job <{HiveLog.Job.IdParam}>", backgroundJobId);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(DeletePropertiesAsync)}.{properties.Length}"), x =>
            {
                return x.Delete<BackgroundJobPropertyTable>().From(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable))
                        .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)).And
                                     .Column(c => c.Name).In.Parameters(properties.Select((x, i) => $"{nameof(properties)}{i}")));
            });
            _logger.Trace($"Deleting <{properties.Length}> properties for background job <{HiveLog.Job.IdParam}> using query <{query}>", backgroundJobId);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(backgroundJobId);
            properties.Execute((i, x) => parameters.AddPropertyName(x, $"{nameof(properties)}{i}"));

            var deleted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token));
            if (deleted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be deleted but only <{deleted}> were deleted");
            _logger.Log($"Deleting <{deleted}> properties for background job <{HiveLog.Job.IdParam}>", backgroundJobId);
        }

        /// <inheritdoc/>
        public virtual async Task<BackgroundJobStorageData> GetBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));

            var backgroundJob = (await GetBackgroundJobsByIdsAsync(connection, id.ConvertTo<long>().AsEnumerable(), token).ConfigureAwait(false)).FirstOrDefault();

            // Convert to storage format
            if (backgroundJob != null)
            {
                _logger.Log($"Selected background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return backgroundJob;
            }
            else
            {
                _logger.Log($"Could not select background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return null;
            }
        }
        /// <inheritdoc/>
        public virtual async Task<(bool WasLocked, BackgroundJobStorageData Data)> TryLockAndTryGetBackgroundJobAsync(string id, string requester, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Trying to set lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{requester}>", id, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryLockAndTryGetBackgroundJobAsync)), x =>
            {
                var update = x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Set.Column(c => c.LockedBy).To.Parameter(nameof(requester))
                              .Set.Column(c => c.LockedAt).To.CurrentDate(DateType.Utc)
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester))));

                var ifUpdated = x.If().Condition(x => x.RowCount().GreaterThan.Value(0))
                                      .Then(x.Select().Value(1))
                                 .Else
                                      .Then(x.Select().Value(0));

                var select = x.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                                .AllOf<BackgroundJobTable>()
                                .AllOf<BackgroundJobStateTable>()
                                .AllOf<BackgroundJobPropertyTable>()
                                .InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobStateTable>(c => c.BackgroundJobId))
                                .LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                                .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(id)))
                                .OrderBy<BackgroundJobStateTable>(c => c.ElectedDate, SortOrders.Ascending);

                return x.New().Append(update).Append(ifUpdated).Append(select);
            });
            _logger.Trace($"Trying to set lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{requester}> using query <{query}>", id, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(requester, nameof(requester));
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var wasUpdated = await reader.ReadSingleAsync<bool>().ConfigureAwait(false);
            var backgroundJob = ReadBackgroundJobs(reader, storageConnection.Environment).FirstOrDefault();
            _logger.Log($"Tried to set lock on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{requester}>", id, connection.Environment);
            return (wasUpdated, backgroundJob);
        }
        /// <inheritdoc/>
        public virtual async Task<(bool WasExtended, LockStorageData Data)> TryHeartbeatLockOnBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to set lock heartbeat on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryHeartbeatLockOnBackgroundJobAsync)), x =>
            {
                var update = x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));

                var ifUpdated = x.If().Condition(x => x.RowCount().GreaterThan.Value(0))
                                      .Then(x.Select().Value(1))
                                 .Else
                                      .Then(x.Select().Value(0));

                var select = x.Select<BackgroundJobTable>()
                              .Column(c => c.Id)
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                return x.New().Append(update).Append(ifUpdated).Append(select);
            });
            _logger.Trace($"Trying to set lock heartbeat on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var wasExtended = await reader.ReadSingleAsync<bool>().ConfigureAwait(false);

            if (wasExtended)
            {
                _logger.Log($"Set lock heartbeat on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            }
            else
            {
                _logger.Warning($"Could not extend lock heartbeat on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            }

            var job = await reader.ReadSingleOrDefaultAsync<BackgroundJobTable>().ConfigureAwait(false);

            if (job.Id > 0)
            {
                return (wasExtended, job.ToLockStorageFormat());
            }

            return (wasExtended, null);
        }
        /// <inheritdoc/>
        public virtual async Task<bool> UnlockBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to remove lock from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UnlockBackgroundJobAsync)), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying remove lock from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed lock from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
                return true;
            }
            else
            {
                _logger.Warning($"Could not remove lock from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{holder}>", id, connection.Environment, holder);
                return false;
            }
        }
        /// <inheritdoc/>
        public virtual async Task UnlockBackgroundsJobAsync(string[] ids, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            var jobIds = ids.Select(x => x.ConvertTo<long>()).ToArray();
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Trying to remove locks from <{ids.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UnlockBackgroundsJobAsync)}.{ids.Length}"), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).In.Parameters(jobIds.Select((x, i) => $"{nameof(jobIds)}{i}")).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying to remove locks from <{ids.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> using query <{query}>", connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddLocker(holder, nameof(holder));
            jobIds.Execute((i, x) => parameters.AddBackgroundJobId(x, $"{nameof(jobIds)}{i}"));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed locks from <{ids.Length}> background jobs of the total <{ids.Length}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", connection.Environment, holder);
            }
            else
            {
                _logger.Warning($"Could not remove any locks from the <{ids.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> for <{holder}>", connection.Environment);
            }
        }
        /// <inheritdoc/>
        public virtual async Task<BackgroundJobStorageData[]> SearchBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Selecting the next max <{pageSize}> background jobs from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated search query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedSearchQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildSearchQuery(_queryProvider, queryConditions, pageSize, page, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }

            parameters.Add(nameof(pageSize), pageSize);
            parameters.Add(nameof(page), pageSize * (page - 1));

            _logger.Trace($"Selecting the next max <{pageSize}> background jobs from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            Dictionary<long, (BackgroundJobTable Job, List<BackgroundJobStateTable> States, List<BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (BackgroundJobTable Job, List<BackgroundJobStateTable> States, List<BackgroundJobPropertyTable> Properties)>();

            _ = await storageConnection.MySqlConnection.QueryAsync<BackgroundJobTable, BackgroundJobStateTable, BackgroundJobPropertyTable, Null>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token),
                (b, s, p) =>
                {
                    // Job
                    List<BackgroundJobStateTable> states = null;
                    List<BackgroundJobPropertyTable> properties = null;
                    if (!backgroundJobs.TryGetValue(b.Id, out var backgroundJob))
                    {
                        states = new List<BackgroundJobStateTable>();
                        properties = new List<BackgroundJobPropertyTable>();
                        backgroundJobs.Add(b.Id, (b, states, properties));
                    }
                    else
                    {
                        b = backgroundJob.Job;
                        states = backgroundJob.States;
                        properties = backgroundJob.Properties;
                    }

                    // State
                    if (!states.Any(x => x.Id == s.Id)) states.Add(s);

                    // Property
                    if (p != null && !properties.Select(x => x.Name).Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        properties.Add(p);
                    }

                    return Null.Value;
                }, $"{nameof(BackgroundJobStateTable.Id)},{nameof(BackgroundJobPropertyTable.BackgroundJobId)}").ConfigureAwait(false);

            // Convert to storage format
            List<BackgroundJobStorageData> jobStorageData = new List<BackgroundJobStorageData>();
            foreach (var backgroundJob in backgroundJobs)
            {
                var job = backgroundJob.Value.Job.ToStorageFormat(_hiveOptions.Get(connection.Environment), _cache);
                job.Lock = backgroundJob.Value.Job.ToLockStorageFormat();
                job.States = backgroundJob.Value.States.Select(x =>
                {
                    var state = x.ToStorageFormat();
                    return state;
                }).ToList();
                if (backgroundJob.Value.Properties.HasValue()) job.Properties = backgroundJob.Value.Properties.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> because it matched the query condition <{queryConditions}>", job.Id, connection.Environment);
                jobStorageData.Add(job);
            }

            _logger.Log($"Selected <{jobStorageData.Count}> background jobs from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return jobStorageData.ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<long> CountBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Counting the amount of background jobs in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated count query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedCountQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildCountQuery(x, queryConditions, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }

            _logger.Trace($"Counting the amount of background jobs in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var total = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Counted <{total}> background jobs in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return total;
        }
        /// <inheritdoc/>
        public virtual async Task<BackgroundJobStorageData[]> LockBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Trying to lock the next <{limit}> background jobs in environment <{HiveLog.EnvironmentParam}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated lock query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"{nameof(LockBackgroundJobsAsync)}.GeneratedSearchAndLockQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildSearchAndLockQuery(_queryProvider, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }
            parameters.Add(nameof(limit), limit);
            parameters.Add(nameof(requester), requester);

            _logger.Trace($"Selecting the ids of the next <{limit}> background jobs in environment <{HiveLog.EnvironmentParam}> to lock for <{requester}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            var ids = await storageConnection.MySqlConnection.QueryAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (!ids.HasValue())
            {
                _logger.Log($"Locked no background jobs in environment <{HiveLog.EnvironmentParam}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);
                return Array.Empty<BackgroundJobStorageData>();
            }

            // Update matching jobs
            _ = await UpdateBackgroundJobLocksByIdsAsync(connection, ids, requester, token).ConfigureAwait(false);

            // Select updated background jobs
            var jobStorageData = await GetBackgroundJobsByIdsAsync(connection, ids, token).ConfigureAwait(false);

            _logger.Log($"Locked <{jobStorageData.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> matching the query condition <{queryConditions}>", storageConnection.Environment, requester);
            return jobStorageData.ToArray();
        }
        private string BuildSearchAndLockQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            bool joinProperty = false;
            bool joinState = false;

            // Select the ids to update because MariaDB update refuses to use the same index as selects and it rather wants to scan the whole table
            var selectIdQuery = queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable)).Column(x => x.Id)
                                              .Where(x =>
                                              {
                                                  var builder = x.WhereGroup(x =>
                                                  {
                                                      (joinProperty, joinState) = BuildWhereStatement<BackgroundJobTable, long, BackgroundJobPropertyTable, BackgroundJobStateTable>(x, parameters, queryConditions.Conditions, nameof(BackgroundJobPropertyTable.BackgroundJobId));
                                                      return x.LastBuilder;
                                                  });

                                                  if (allowAlreadyLocked)
                                                  {
                                                      return builder.And.WhereGroup(x => x.Column(x => x.LockedBy).IsNull
                                                                                          .Or.Column(x => x.LockedBy).EqualTo.Parameter(nameof(requester)));
                                                  }
                                                  else
                                                  {
                                                      return builder.And.Column(x => x.LockedBy).IsNull;
                                                  }
                                              })
                                              .ForUpdateSkipLocked()
                                              .Limit(x => x.Parameter(nameof(limit)));

            // Join if needed
            if (joinProperty) selectIdQuery.LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) selectIdQuery.InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobStateTable>(x => x.BackgroundJobId));

            if (orderBy.HasValue)
            {
                QueryBackgroundJobOrderByTarget orderByTarget = orderBy.Value;
                switch (orderByTarget)
                {
                    case QueryBackgroundJobOrderByTarget.Id:
                        selectIdQuery.OrderBy(x => x.Id, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.Queue:
                        selectIdQuery.OrderBy(x => x.Queue, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.Priority:
                        selectIdQuery.OrderBy(x => x.Priority, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.CreatedAt:
                        selectIdQuery.OrderBy(x => x.CreatedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.ModifiedAt:
                        selectIdQuery.OrderBy(x => x.ModifiedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    default: throw new NotSupportedException($"Order by target <{orderByTarget}> is not supported");
                }
            }

            // Determine what to update and keep an update lock
            return selectIdQuery.Build(_compileOptions);
        }
        private string BuildSearchQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            bool joinProperty = false;
            bool joinState = false;


            // Select id of matching
            var selectIdQuery = queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable)).Column(x => x.Id)
                                             .Where(x =>
                                             {
                                                 (joinProperty, joinState) = BuildWhereStatement<BackgroundJobTable, long, BackgroundJobPropertyTable, BackgroundJobStateTable>(x, parameters, queryConditions.Conditions, nameof(BackgroundJobPropertyTable.BackgroundJobId));
                                                 return x.LastBuilder;
                                             })
                                             .Limit(x => x.Parameter(nameof(page)), x => x.Parameter(nameof(pageSize)));

            // Join if needed
            if (joinProperty) selectIdQuery.LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) selectIdQuery.InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobStateTable>(x => x.BackgroundJobId));

            if (orderBy.HasValue)
            {
                QueryBackgroundJobOrderByTarget orderByTarget = orderBy.Value;
                switch (orderByTarget)
                {
                    case QueryBackgroundJobOrderByTarget.Id:
                        selectIdQuery.OrderBy(x => x.Id, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.Queue:
                        selectIdQuery.OrderBy(x => x.Queue, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.Priority:
                        selectIdQuery.OrderBy(x => x.Priority, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.CreatedAt:
                        selectIdQuery.OrderBy(x => x.CreatedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryBackgroundJobOrderByTarget.ModifiedAt:
                        selectIdQuery.OrderBy(x => x.ModifiedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    default: throw new NotSupportedException($"Order by target <{orderByTarget}> is not supported");
                }
            }

            // Select background jobs
            var selectQuery = queryProvider.With().Cte("cte")
                                                        .As(selectIdQuery)
                                                   .Execute(queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                                                                            .AllOf<BackgroundJobTable>()
                                                                            .AllOf<BackgroundJobStateTable>()
                                                                            .AllOf<BackgroundJobPropertyTable>()
                                                                            .InnerJoin().Table("cte", 'c').On(x => x.Column(x => x.Id).EqualTo.Column('c', x => x.Id))
                                                                            .InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobStateTable>(c => c.BackgroundJobId))
                                                                            .LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                                                                            .OrderBy<BackgroundJobStateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                                                                            .OrderBy<BackgroundJobStateTable>(c => c.ElectedDate, SortOrders.Ascending));

            return selectQuery.Build(_compileOptions);
        }
        private string BuildCountQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));

            bool joinProperty = false;
            bool joinState = false;

            var countQuery = queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable));
            countQuery.Where(x =>
            {
                (joinProperty, joinState) = BuildWhereStatement<BackgroundJobTable, long, BackgroundJobPropertyTable, BackgroundJobStateTable>(x, parameters, queryConditions.Conditions, nameof(BackgroundJobPropertyTable.BackgroundJobId));
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobStateTable>(x => x.BackgroundJobId));

            // Count total matching
            countQuery.Count(x => x.Id);

            return countQuery.Build(_compileOptions);
        }

        /// <inheritdoc/>
        public virtual async Task CreateBackgroundJobLogsAsync(IStorageConnection connection, string id, IEnumerable<LogEntry> logEntries, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            logEntries.ValidateArgumentNotNullOrEmpty(nameof(logEntries));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var count = logEntries.GetCount();
            var backgroundJobId = id.ConvertTo<long>();
            _logger.Log($"Inserting <{count}> log entries for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(CreateBackgroundJobLogsAsync)}.{count}"), x =>
            {
                var insertQuery = x.Insert<BackgroundJobLogTable>().Into(table: TableNames.BackgroundJobLogTable).ColumnsOf(nameof(BackgroundJobLogTable.CreatedAtUtc));
                logEntries.Execute((i, x) =>
                {
                    insertQuery.Values(x => x.Parameter(p => p.BackgroundJobId, i.ToString())
                                      , x => x.Parameter(p => p.LogLevel, i.ToString())
                                      , x => x.Parameter(p => p.Message, i.ToString())
                                      , x => x.Parameter(p => p.ExceptionType, i.ToString())
                                      , x => x.Parameter(p => p.ExceptionMessage, i.ToString())
                                      , x => x.Parameter(p => p.ExceptionStackTrace, i.ToString())
                                      , x => x.Parameter(p => p.CreatedAt, i.ToString()));
                });
                return insertQuery;
            });
            _logger.Trace($"Inserting <{count}> log entries for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            logEntries.Execute((i, x) => new BackgroundJobLogTable(backgroundJobId, x).AppendCreateParameters(parameters, i));

            var inserted = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Inserted <{inserted}> log entries for background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<LogEntry[]> GetBackgroundJobLogsAsync(IStorageConnection connection, string id, LogLevel[] logLevels, int page, int pageSize, bool mostRecentFirst, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            page.ValidateArgumentLarger(nameof(page), 0);
            pageSize.ValidateArgumentLarger(nameof(pageSize), 1);
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Fetching up to <{pageSize}> logs from page <{page}> of background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetBackgroundJobLogsAsync)}.{logLevels?.Length ?? 0}.{mostRecentFirst}"), x =>
            {
                var getQuery = x.Select<BackgroundJobLogTable>().All()
                                .From()
                                .Limit(SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(page)), SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(pageSize)))
                                .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(id)))
                                .OrderBy(x => x.CreatedAt, mostRecentFirst ? SortOrders.Descending : SortOrders.Ascending);

                if (logLevels.HasValue()) getQuery.Where(x => x.Column(x => x.LogLevel).In.Parameters(logLevels.Select((i, x) => $"{nameof(logLevels)}{i}")));
                return getQuery;
            });
            _logger.Trace($"Fetching up to <{pageSize}> logs from page <{page}> of background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>());
            parameters.AddPage(page, pageSize);
            parameters.AddPageSize(pageSize);
            if (logLevels.HasValue()) logLevels.Execute((i, x) => parameters.Add($"{nameof(logLevels)}{i}", x, DbType.Int32, ParameterDirection.Input));

            var logs = (await storageConnection.MySqlConnection.QueryAsync<LogEntry>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Fetched <{logs.Length}> logs from page <{page}> of background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            return logs;
        }
        /// <inheritdoc/>
        public virtual async Task<(bool Exists, string Data)> TryGetBackgroundJobDataAsync(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to get data <{name}> from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryGetBackgroundJobDataAsync)), x =>
            {
                return x.Select<BackgroundJobDataTable>().Column(c => c.Value)
                        .From()
                        .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(id))
                                 .And.Column(c => c.Name).EqualTo.Parameter(nameof(name)));
            });
            _logger.Trace($"Trying to get data <{name}> from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddDataName(name);

            var value = await storageConnection.MySqlConnection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Fetched data <{name}> from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>: {value ?? "NULL"}", id, storageConnection.Environment);
            return (value != null, value);
        }
        /// <inheritdoc/>
        public virtual async Task SetBackgroundJobDataAsync(IStorageConnection connection, string id, string name, string value, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Saving data <{name}> to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(SetBackgroundJobDataAsync)), b =>
            {
                var updateQuery = b.Update<BackgroundJobDataTable>().Table()
                                   .Set.Column(c => c.Value).To.Parameter(p => p.Value)
                                   .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(p => p.BackgroundJobId)
                                            .And.Column(c => c.Name).EqualTo.Parameter(p => p.Name));

                // Insert if update did not update anything
                var insertQuery = b.If().Condition(x => x.RowCount().EqualTo.Value(0))
                                        .Then(x => x.Append(b.Insert<BackgroundJobDataTable>().Into().Columns(c => c.BackgroundJobId, c => c.Name, c => c.Value)
                                                             .Parameters(p => p.BackgroundJobId, p => p.Name, p => p.Value)
                                                           )
                                        );

                return b.New().Append(updateQuery).Append(insertQuery);
            });
            _logger.Trace($"Saving data <{name}> to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var table = new BackgroundJobDataTable()
            {
                BackgroundJobId = id.ConvertTo<long>(),
                Name = name,
                Value = value
            };
            var parameters = table.ToCreateParameters();
            await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<BackgroundJobStorageData[]> GetTimedOutBackgroundJobs(IStorageConnection connection, int limit, string requester, TimeSpan timeoutThreshold, CancellationToken token = default)
        {
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            // Get ids to update
            _logger.Log($"Selecting at most <{limit}> background jobs where the lock timed out in environment <{HiveLog.EnvironmentParam}> for <{requester}> with update lock", connection.Environment);
            var selectIdQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetTimedOutBackgroundJobs)}.Select"), x =>
            {
                return x.Select<BackgroundJobTable>().From(table: TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Column(x => x.Id)
                        .Where(x => x.Column(x => x.LockedBy).IsNotNull.And
                                     .Column(x => x.LockHeartbeat).LesserThan.ModifyDate(x => x.CurrentDate(DateType.Utc), x => x.Parameter(nameof(timeoutThreshold)), DateInterval.Millisecond)
                              )
                        .ForUpdateSkipLocked()
                        .Limit(x => x.Parameter(nameof(limit)));
            });
            _logger.Trace($"Selecting at most <{limit}> background jobs where the lock timed out in environment <{HiveLog.EnvironmentParam}> for <{requester}> with update lock using query <{selectIdQuery}>", connection.Environment);

            var parameters = new DynamicParameters();
            parameters.AddLimit(limit);
            parameters.Add(nameof(timeoutThreshold), -timeoutThreshold.TotalMilliseconds, DbType.Double, ParameterDirection.Input);
            var ids = (await storageConnection.MySqlConnection.QueryAsync<long>(new CommandDefinition(selectIdQuery, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            if (!ids.HasValue())
            {
                _logger.Log($"No timed out background jobs in environment <{HiveLog.EnvironmentParam}> locked for <{requester}>", connection.Environment);
                return Array.Empty<BackgroundJobStorageData>();
            }

            // Update new lock owner
            _ = await UpdateBackgroundJobLocksByIdsAsync(connection, ids, requester, token).ConfigureAwait(false);

            // Select background jobs
            return await GetBackgroundJobsByIdsAsync(connection, ids, token).ConfigureAwait(false);
        }

        private async Task<BackgroundJobStorageData[]> GetBackgroundJobsByIdsAsync(IStorageConnection connection, IEnumerable<long> ids, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));

            _logger.Log($"Selecting <{ids.GetCount()}> background jobs by id in environment <{HiveLog.EnvironmentParam}>", storageConnection.Environment);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetBackgroundJobsByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return x.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .AllOf<BackgroundJobTable>()
                        .AllOf<BackgroundJobStateTable>()
                        .AllOf<BackgroundJobPropertyTable>()
                        .InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobStateTable>(c => c.BackgroundJobId))
                        .LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                        .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")))
                        .OrderBy<BackgroundJobStateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                        .OrderBy<BackgroundJobStateTable>(c => c.ElectedDate, SortOrders.Ascending);
            });
            _logger.Trace($"Selecting <{ids.GetCount()}> background jobs by id in environment <{HiveLog.EnvironmentParam}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            ids.Execute((i, x) => parameters.AddBackgroundJobId(x, $"Id{i}"));

            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            return ReadBackgroundJobs(reader, storageConnection.Environment);
        }

        private async Task<int> UpdateBackgroundJobLocksByIdsAsync(IStorageConnection connection, IEnumerable<long> ids, string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));

            // Generate query
            _logger.Log($"Updating <{ids.GetCount()}> background jobs locks by id in environment <{HiveLog.EnvironmentParam}> so they are held by <{holder}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UpdateBackgroundJobLocksByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(x => x.LockedBy).To.Parameter(nameof(holder))
                        .Set.Column(x => x.LockHeartbeat).To.CurrentDate(DateType.Utc)
                        .Set.Column(x => x.LockedAt).To.CurrentDate(DateType.Utc)
                        .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")));
            });
            _logger.Trace($"Updating <{ids.GetCount()}> background jobs locks by id in environment <{HiveLog.EnvironmentParam}> so they are held by <{holder}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddLocker(holder, nameof(holder));
            ids.Execute((i, x) => parameters.AddBackgroundJobId(x, $"Id{i}"));

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Updated <{updated}> background jobs locks by id in environment <{HiveLog.EnvironmentParam}> so they are now held by <{HiveLog.Job.LockHolderParam}>", storageConnection.Environment, holder);
            return updated;
        }

        /// <inheritdoc/>
        public virtual async Task<string[]> GetAllBackgroundJobQueuesAsync(IStorageConnection connection, string prefix = null, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var filterByPrefix = prefix.HasValue();
            _logger.Log($"Selecting all distinct background job queues from environment <{HiveLog.EnvironmentParam}> matching prefix <{prefix}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetAllBackgroundJobQueuesAsync)}.{filterByPrefix}"), x =>
            {
                var select = x.Select<BackgroundJobTable>()
                            .Distinct().Column(x => x.Queue)
                            .From(table: TableNames.BackgroundJobTable, typeof(BackgroundJobTable));
                if (filterByPrefix) select.Where(x => x.Column(x => x.Queue).Like.Concat(StaticSql.Expressions.Parameter(nameof(prefix)), '%'));
                return select;
            });
            _logger.Trace($"Selecting all distinct background job queues from environment <{HiveLog.EnvironmentParam}> matching prefix <{prefix}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            if (filterByPrefix) parameters.Add(nameof(prefix), prefix, DbType.String, size: 255);
            var queues = (await storageConnection.MySqlConnection.QueryAsync<string>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Selected <{queues.Length}> distinct background job queues from environment <{HiveLog.EnvironmentParam}> matching prefix <{prefix}>", storageConnection.Environment);
            return queues;
        }

        /// <inheritdoc/>
        public virtual async Task<bool> TryDeleteBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Deleting background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if it is still held by <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryDeleteBackgroundJobAsync)), x =>
            {
                return x.Delete<BackgroundJobTable>().From(table: TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(id)).And
                                     .Column(x => x.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Deleting background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if it is still held by <{HiveLog.Job.LockHolderParam}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(holder, nameof(holder));

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Deletion of background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam} held by <{HiveLog.Job.LockHolderParam}> was <{wasDeleted}>", id, connection.Environment, holder);

            return wasDeleted;
        }

        /// <inheritdoc/>
        public virtual async Task<string[]> TryDeleteBackgroundJobsAsync(string[] ids, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Deleting <{ids.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> if it is still held by <{HiveLog.Job.LockHolderParam}>", connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TryDeleteBackgroundJobsAsync)}.{ids.Length}"), x =>
            {
                return x.Delete<BackgroundJobTable>().From(table: TableNames.BackgroundJobTable)
                        .Returning(x => x.Column(null, c => c.Id))
                        .Where(x => x.Column(null, x => x.Id).In.Parameters(ids.Select((x, i) => $"Ids{i}")).And
                                     .Column(null, x => x.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Deleting <{ids.Length}> background jobs in environment <{HiveLog.EnvironmentParam}> if it is still held by <{HiveLog.Job.LockHolderParam}> using query <{query}>", connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            ids.Execute((i, x) => parameters.AddBackgroundJobId(x.ConvertTo<long>(), $"Ids{i}"));
            parameters.AddLocker(holder, nameof(holder));

            var deletedIds = (await storageConnection.MySqlConnection.QueryAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Deleted <{deletedIds.Length}> background jobs in environment <{HiveLog.EnvironmentParam} held by <{HiveLog.Job.LockHolderParam}>", connection.Environment, holder);

            return deletedIds.Select(x => x.ToString()).ToArray();
        }

        /// <inheritdoc/>
        public virtual async Task CreateBackgroundJobActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            action.ValidateArgument(nameof(action));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Creating new action on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateBackgroundJobActionAsync)), x =>
            {
                return x.Insert<BackgroundJobActionTable>().Into(table: TableNames.BackgroundJobActionTable).Columns(x => x.BackgroundJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAt)
                        .Parameters(x => x.BackgroundJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAt);
            });
            _logger.Trace($"Creating new action on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", action.ComponentId, connection.Environment);

            // Execute query
            var backgroundJobAction = new BackgroundJobActionTable(action, _hiveOptions.Get(_environment), _cache);
            var parameters = backgroundJobAction.ToCreateParameters();

            _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Created new action on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<ActionInfo[]> GetNextBackgroundJobActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var backgroundJobId = id.ConvertTo<long>();
            _logger.Log($"Fetching the next <{limit}> pending actions on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", backgroundJobId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetNextBackgroundJobActionsAsync)), x =>
            {
                return x.Select<BackgroundJobActionTable>().All().From(TableNames.BackgroundJobActionTable, typeof(BackgroundJobActionTable))
                       .Where(x => x.Column(x => x.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)))
                       .OrderBy(x => x.Priority, SortOrders.Ascending).OrderBy(x => x.CreatedAt, SortOrders.Ascending)
                       .Limit(x => x.Parameter(nameof(limit)));
            });
            _logger.Trace($"Fetching the next <{limit}> pending actions on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", backgroundJobId, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(backgroundJobId);
            parameters.AddLimit(limit);
            var actions = (await storageConnection.MySqlConnection.QueryAsync<BackgroundJobActionTable>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Fetched <{actions?.Length}> pending actions on background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> ", backgroundJobId, connection.Environment);
            return actions.Select(x => x.ToAction(_hiveOptions.Get(_environment), _cache)).ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<bool> DeleteBackgroundJobActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var actionId = id.ConvertTo<long>();
            _logger.Log($"Removing background job action <{actionId}> in environment <{HiveLog.EnvironmentParam}>", actionId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(DeleteBackgroundJobActionByIdAsync)), x =>
            {
                return x.Delete<BackgroundJobActionTable>().From(TableNames.BackgroundJobActionTable, typeof(BackgroundJobActionTable))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(actionId)));
            });
            _logger.Trace($"Removing background job action <{actionId}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", actionId, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(actionId), actionId);

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Removing of background job action <{actionId}> in environment <{HiveLog.EnvironmentParam}> was <{wasDeleted}>", actionId, connection.Environment);
            return wasDeleted;
        }

        protected BackgroundJobStorageData[] ReadBackgroundJobs(SqlMapper.GridReader reader, string environment)
        {
            reader.ValidateArgument(nameof(reader));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Dictionary<long, (BackgroundJobTable Job, List<BackgroundJobStateTable> States, Dictionary<string, BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (BackgroundJobTable Job, List<BackgroundJobStateTable> States, Dictionary<string, BackgroundJobPropertyTable> Properties)>();

            _ = reader.Read<BackgroundJobTable, BackgroundJobStateTable, BackgroundJobPropertyTable, Null>((b, s, p) =>
            {
                // Job
                List<BackgroundJobStateTable> states = null;
                Dictionary<string, BackgroundJobPropertyTable> properties = null;
                if (!backgroundJobs.TryGetValue(b.Id, out var backgroundJob))
                {
                    states = new List<BackgroundJobStateTable>();
                    properties = new Dictionary<string, BackgroundJobPropertyTable>(StringComparer.OrdinalIgnoreCase);
                    backgroundJobs.Add(b.Id, (b, states, properties));
                }
                else
                {
                    b = backgroundJob.Job;
                    states = backgroundJob.States;
                    properties = backgroundJob.Properties;
                }

                // State
                if (!states.Any(x => x.Id == s.Id)) states.Add(s);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(BackgroundJobStateTable.Id)},{nameof(BackgroundJobPropertyTable.BackgroundJobId)}");

            // Convert to storage format
            List<BackgroundJobStorageData> jobStorageData = new List<BackgroundJobStorageData>();

            foreach (var backgroundJob in backgroundJobs)
            {
                var job = backgroundJob.Value.Job.ToStorageFormat(_hiveOptions.Get(environment), _cache);
                job.Lock = backgroundJob.Value.Job.ToLockStorageFormat();
                job.States = backgroundJob.Value.States.Select(x =>
                {
                    var state = x.ToStorageFormat();
                    return state;
                }).ToList();
                if (backgroundJob.Value.Properties.HasValue()) job.Properties = backgroundJob.Value.Properties.Values.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected background job job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", job.Id, environment);
                jobStorageData.Add(job);
            }

            return jobStorageData.ToArray();
        }
        #endregion

        #region RecurringJob
        /// <summary>
        /// Returns a query builder that contains a select statement for recurring jobs.
        /// </summary>
        /// <param name="provider">The provider to use to build the query</param>
        /// <returns>A select statement for a recuring job</returns>
        protected ISelectStatementBuilder<RecurringJobTable> SelectRecurringJob(ISqlQueryProvider provider)
            => provider.ValidateArgument(nameof(provider))
                       .Select<RecurringJobTable>()
                       .AllOf<RecurringJobTable>()
                       .AllOf<RecurringJobStateTable>()
                       .AllOf<RecurringJobPropertyTable>()
                       .From()
                       .LeftJoin().Table<RecurringJobPropertyTable>().On(x => x.Column(c => c.Id).EqualTo.Column<RecurringJobPropertyTable>(c => c.RecurringJobId))
                       .LeftJoin().Table<RecurringJobStateTable>().On(x => x.Column(c => c.Id).EqualTo.Column<RecurringJobStateTable>(c => c.RecurringJobId))
                       .OrderBy<RecurringJobStateTable>(c => c.ElectedDate);
        /// <summary>
        /// Returns a query builder that contains a select statement for a recuring job fetched by id.
        /// </summary>
        /// <param name="provider">The provider to use to build the query</param>
        /// <returns>A select statement for a recuring job fetched by id</returns>
        protected ISelectStatementBuilder<RecurringJobTable> SelectRecurringJobById(ISqlQueryProvider provider)
            => SelectRecurringJob(provider)
               .Where(x => x.Column(c => c.Id).EqualTo.Parameter(x => x.Id));
        /// <inheritdoc/>
        public virtual async Task<RecurringJobStorageData> TryCreateAsync(IStorageConnection connection, RecurringJobConfigurationStorageData storageData, CancellationToken token = default)
        {
            storageData.ValidateArgument(nameof(storageData));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Inserting recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if it does not exist yet", storageData.Id, _environment);
            // Job
            var job = new RecurringJobTable(storageData, _hiveOptions.Get(connection.Environment), _cache);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryCreateAsync)), x =>
            {
                var exists = x.Select<RecurringJobTable>().Value(1).Column(x => x.Id).From().Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(job.Id)));

                var insert = x.Insert<RecurringJobTable>().Into()
                              .Columns(x => x.Id, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.Schedule, x => x.LockedBy, x => x.LockedAt, x => x.LockHeartbeat, x => x.Settings, x => x.CreatedAt, x => x.ModifiedAt)
                              .Parameters(x => x.Id, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.Schedule, x => x.LockedBy, x => x.LockedAt, x => x.LockHeartbeat, x => x.Settings, x => x.CreatedAt, x => x.ModifiedAt);

                var ifNotExists = x.If().Condition(x => x.Not().ExistsIn(exists)).Then(x => x.Append(insert));

                var select = SelectRecurringJobById(x);

                return x.New().Append(ifNotExists).Append(select);
            });
            var parameters = job.ToCreateParameters();
            _logger.Trace($"Inserting recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if it does not exist yet using query <{query}>", _environment);

            var recurringJob = (await QueryRecurringJobs(storageConnection, new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).Single();

            if (recurringJob.States.HasValue())
            {
                _logger.Log($"Could not insert recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> because it already exists", storageData.Id, _environment);
            }
            else
            {
                _logger.Log($"Inserted recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", storageData.Id, _environment);
            }

            return recurringJob;
        }
        /// <inheritdoc/>
        public virtual async Task<RecurringJobStorageData> GetRecurringJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection, false);

            _logger.Log($"Selecting recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);

            var query = _queryProvider.GetQuery(nameof(GetRecurringJobAsync), x => SelectRecurringJobById(x));

            _logger.Log($"Selecting recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, connection.Environment);

            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            var recurringJob = (await QueryRecurringJobs(storageConnection, new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).FirstOrDefault();

            if (recurringJob != null)
            {
                _logger.Log($"Selected recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return recurringJob;
            }
            else
            {
                _logger.Log($"Could not select recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
                return null;
            }
        }
        /// <summary>
        /// Executes <paramref name="command"/> using <paramref name="storageConnection"/> and returns the result as an array of <see cref="RecurringJobStorageData"/>.
        /// </summary>
        /// <param name="storageConnection">The storage connection to use to execute <paramref name="storageConnection"/></param>
        /// <param name="command">The command to execute</param>
        /// <returns>Any <see cref="RecurringJobStorageData"/> returned by the query</returns>
        protected async Task<RecurringJobStorageData[]> QueryRecurringJobs(MySqlStorageConnection storageConnection, CommandDefinition command)
        {
            Dictionary<string, (RecurringJobTable Job, List<RecurringJobStateTable> States, Dictionary<string, RecurringJobPropertyTable> Properties)> recurringJobs = new Dictionary<string, (RecurringJobTable Job, List<RecurringJobStateTable> States, Dictionary<string, RecurringJobPropertyTable> Properties)>();
            _ = await storageConnection.MySqlConnection.QueryAsync(command, (RecurringJobTable b, RecurringJobStateTable s, RecurringJobPropertyTable p) =>
            {
                // Job
                if (b == null) return Null.Value;
                List<RecurringJobStateTable> states = null;
                Dictionary<string, RecurringJobPropertyTable> properties = null;
                if (!recurringJobs.TryGetValue(b.Id, out var recurringJob))
                {
                    states = new List<RecurringJobStateTable>();
                    properties = new Dictionary<string, RecurringJobPropertyTable>();
                    recurringJobs.Add(b.Id, (b, states, properties));
                }
                else
                {
                    b = recurringJob.Job;
                    states = recurringJob.States;
                    properties = recurringJob.Properties;
                }

                // State
                if (s != null && !states.Any(x => x.Id == s.Id)) states.Add(s);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(RecurringJobStateTable.Id)},{nameof(RecurringJobPropertyTable.RecurringJobId)}").ConfigureAwait(false);

            // Convert to storage format
            List<RecurringJobStorageData> jobStorageData = new List<RecurringJobStorageData>();

            foreach (var recurringJob in recurringJobs)
            {
                var job = recurringJob.Value.Job.ToStorageFormat(_hiveOptions.Get(storageConnection.Environment), _cache);
                job.Lock = recurringJob.Value.Job.ToLockStorageFormat();
                job.States = recurringJob.Value.States.Select(x =>
                {
                    var state = x.ToStorageFormat();
                    return state;
                }).ToList();
                if (recurringJob.Value.Properties.HasValue()) job.Properties = recurringJob.Value.Properties.Values.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", job.Id, storageConnection.Environment);
                jobStorageData.Add(job);
            }

            return jobStorageData.ToArray();
        }

        protected RecurringJobStorageData[] ReadRecurringJobs(SqlMapper.GridReader reader, string environment)
        {
            reader.ValidateArgument(nameof(reader));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Dictionary<string, (RecurringJobTable Job, List<RecurringJobStateTable> States, Dictionary<string, RecurringJobPropertyTable> Properties)> recurringJobs = new Dictionary<string, (RecurringJobTable Job, List<RecurringJobStateTable> States, Dictionary<string, RecurringJobPropertyTable> Properties)>();
            _ = reader.Read<RecurringJobTable, RecurringJobStateTable, RecurringJobPropertyTable, Null>((RecurringJobTable b, RecurringJobStateTable s, RecurringJobPropertyTable p) =>
            {
                // Job
                if (b == null) return Null.Value;
                List<RecurringJobStateTable> states = null;
                Dictionary<string, RecurringJobPropertyTable> properties = null;
                if (!recurringJobs.TryGetValue(b.Id, out var recurringJob))
                {
                    states = new List<RecurringJobStateTable>();
                    properties = new Dictionary<string, RecurringJobPropertyTable>(StringComparer.OrdinalIgnoreCase);
                    recurringJobs.Add(b.Id, (b, states, properties));
                }
                else
                {
                    b = recurringJob.Job;
                    states = recurringJob.States;
                    properties = recurringJob.Properties;
                }

                // State
                if (s != null && !states.Any(x => x.Id == s.Id)) states.Add(s);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(RecurringJobStateTable.Id)},{nameof(RecurringJobPropertyTable.RecurringJobId)}");

            // Convert to storage format
            List<RecurringJobStorageData> jobStorageData = new List<RecurringJobStorageData>();

            foreach (var recurringJob in recurringJobs)
            {
                var job = recurringJob.Value.Job.ToStorageFormat(_hiveOptions.Get(environment), _cache);
                job.Lock = recurringJob.Value.Job.ToLockStorageFormat();
                job.States = recurringJob.Value.States.Select(x =>
                {
                    var state = x.ToStorageFormat();
                    return state;
                }).ToList();
                if (recurringJob.Value.Properties.HasValue()) job.Properties = recurringJob.Value.Properties.Values.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", job.Id, environment);
                jobStorageData.Add(job);
            }

            return jobStorageData.ToArray();
        }

        /// <inheritdoc/>
        public virtual async Task<bool> TryUpdateRecurringJobAsync(IStorageConnection connection, RecurringJobStorageData jobData, bool releaseLock, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Updating recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", jobData.Id, _environment);
            var holder = jobData.Lock.LockedBy;
            var recurringJob = new RecurringJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryUpdateRecurringJobAsync)), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(c => c.Queue).To.Parameter(c => c.Queue)
                        .Set.Column(c => c.Priority).To.Parameter(c => c.Priority)
                        .Set.Column(c => c.ExecutionId).To.Parameter(c => c.ExecutionId)
                        .Set.Column(c => c.ExecutedAmount).To.Parameter(c => c.ExecutedAmount)
                        .Set.Column(c => c.ExpectedExecutionDate).To.Parameter(c => c.ExpectedExecutionDate)
                        .Set.Column(c => c.LastStartedDate).To.Parameter(c => c.LastStartedDate)
                        .Set.Column(c => c.LastCompletedDate).To.Parameter(c => c.LastCompletedDate)
                        .Set.Column(c => c.ModifiedAt).To.Parameter(c => c.ModifiedAt)
                        .Set.Column(c => c.LockedBy).To.Parameter(c => c.LockedBy)
                        .Set.Column(c => c.LockedAt).To.Parameter(c => c.LockedAt)
                        .Set.Column(c => c.LockHeartbeat).To.Parameter(c => c.LockHeartbeat)
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id)
                                     .And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));

            });
            _logger.Trace($"Updating background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", jobData.Id, _environment);

            // Execute query
            var parameters = recurringJob.ToUpdateParameters(holder, releaseLock);
            parameters.AddLocker(holder, nameof(holder));
            parameters.RemoveUnused = true;

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated != 1)
            {
                _logger.Warning($"Could not update recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", jobData.Id, _environment);
                return false;
            }
            else
            {
                var recurringJobId = jobData.Id;
                // Persist new states
                if (jobData.ChangeTracker.NewStates.HasValue())
                {
                    await InsertStatesAsync(storageConnection, recurringJobId, jobData.ChangeTracker.NewStates.Select(x => new RecurringJobStateTable(x)).ToArray(), token).ConfigureAwait(false);
                }

                // Persist changes to properties
                if (jobData.ChangeTracker.NewProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.NewProperties.Select(x => new RecurringJobPropertyTable(x)).ToArray();
                    await InsertPropertiesAsync(storageConnection, recurringJobId, properties, token).ConfigureAwait(false);
                }
                if (jobData.ChangeTracker.UpdatedProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.UpdatedProperties.Select(x => new RecurringJobPropertyTable(x)).ToArray();
                    await UpdatePropertiesAsync(storageConnection, recurringJobId, properties, token).ConfigureAwait(false);
                }
                if (jobData.ChangeTracker.RemovedProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.RemovedProperties.ToArray();
                    await DeletePropertiesAsync(storageConnection, recurringJobId, properties, token).ConfigureAwait(false);
                }

                return true;
            }
        }
        /// <inheritdoc/>
        public virtual async Task<bool> TryDeleteRecurringJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Deleting recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if it is still held by <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryDeleteBackgroundJobAsync)), x =>
            {
                return x.Delete<RecurringJobTable>().From()
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(id)).And
                                     .Column(x => x.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Deleting recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> if it is still held by <{HiveLog.Job.LockHolderParam}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(holder, nameof(holder));

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Deletion of recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam} held by <{HiveLog.Job.LockHolderParam}> was <{wasDeleted}>", id, connection.Environment, holder);

            return wasDeleted;
        }
        /// <summary>
        /// Inserts the new states for recurring job <paramref name="recurringJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="recurringJobId">The id of the recurring job to insert the states for</param>
        /// <param name="states">The states and their properties to insert</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task InsertStatesAsync(MySqlStorageConnection connection, string recurringJobId, RecurringJobStateTable[] states, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            recurringJobId.ValidateArgumentNotNullOrWhitespace(nameof(recurringJobId));
            states.ValidateArgument(nameof(states));
            _logger.Log($"Inserting <{states.Length}> new states for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);

            var parameters = new DynamicParameters();

            // Reset is current on existing states
            var resetQuery = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(InsertStatesWithPropertiesAsync)}.Reset"), x =>
            {
                return x.Update<RecurringJobStateTable>().Table()
                        .Set.Column(c => c.IsCurrent).To.Value(false)
                        .Where(w => w.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(recurringJobId)));
            });

            parameters.AddRecurringJobId(recurringJobId);
            _logger.Trace($"Resetting {nameof(BackgroundJobStateTable.IsCurrent)} to false for existing states for recurring job <{HiveLog.Job.IdParam}> using query <{resetQuery}>", recurringJobId);
            await connection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(resetQuery, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            // Insert new
            parameters = new DynamicParameters();
            states.Last().IsCurrent = true;
            states.Execute((i, x) =>
            {
                x.RecurringJobId = recurringJobId;
                x.AppendCreateParameters(parameters, i.ToString());
            });
            var query = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(InsertStatesWithPropertiesAsync)}.Insert.{states.Length}"), x =>
            {
                var insert = x.Insert<RecurringJobStateTable>().Into()
                              .Columns(c => c.Name, c => c.OriginalType, c => c.RecurringJobId, c => c.Sequence, c => c.ElectedDate, c => c.Reason, c => c.Data, c => c.IsCurrent)
                              .Returning(x => x.Column(c => c.Id));
                Enumerable.Range(0, states.Length).Execute(x => insert.Parameters(x.ToString(), c => c.Name, c => c.OriginalType, c => c.RecurringJobId, c => c.Sequence, c => c.ElectedDate, c => c.Reason, c => c.Data, c => c.IsCurrent));

                return insert;
            });

            _logger.Trace($"Inserting <{states.Length}> new states for recurring job <{HiveLog.Job.IdParam}> using query <{query}>", recurringJobId);
            var ids = (await connection.MySqlConnection.QueryAsync<long>(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();
            ids.Execute((i, x) => states[i].Id = x);

            _logger.Log($"Inserted <{states.Length}> new states for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
        }
        /// <summary>
        /// Inserts properties for recurring job <paramref name="recurringJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="recurringJobId">The id of the job to insert the properties for</param>
        /// <param name="properties">The properties to insert</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task InsertPropertiesAsync(MySqlStorageConnection connection, string recurringJobId, RecurringJobPropertyTable[] properties, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            recurringJobId.ValidateArgumentNotNullOrWhitespace(nameof(recurringJobId));
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Inserting <{properties.Length}> new properties for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
            properties.Execute(x =>
            {
                x.RecurringJobId = recurringJobId;
                x.CreatedAt = DateTime.UtcNow;
                x.ModifiedAt = DateTime.UtcNow;
            });

            var parameters = new DynamicParameters();
            var query = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(InsertPropertiesAsync)}.{properties.Length}"), x =>
            {
                var insertQuery = x.Insert<RecurringJobPropertyTable>().Into()
                                   .Columns(c => c.RecurringJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt);
                Enumerable.Range(0, properties.Length).Execute(x => insertQuery.Parameters(x.ToString(), c => c.RecurringJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt));
                return insertQuery;
            });
            _logger.Trace($"Inserting <{properties.Length}> properties for recurring job <{HiveLog.Job.IdParam}> using query <{query}>", recurringJobId);

            properties.Execute((i, x) => x.AppendCreateParameters(parameters, i.ToString()));
            var inserted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            if (inserted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be inserted but only <{inserted}> were inserted");
            _logger.Log($"Inserted <{inserted}> new properties for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
        }
        /// <summary>
        /// Updates properties for recurring job <paramref name="recurringJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="recurringJobId">The id of the job to insert the properties for</param>
        /// <param name="properties">The properties to update</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task UpdatePropertiesAsync(MySqlStorageConnection connection, string recurringJobId, RecurringJobPropertyTable[] properties, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            recurringJobId.ValidateArgumentNotNullOrWhitespace(nameof(recurringJobId));
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Updating <{properties.Length}> properties for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
            properties.Execute(x =>
            {
                x.ModifiedAt = DateTime.UtcNow;
            });

            // Generate query
            var query = _queryProvider.GetQuery($"Recurring.{GetCacheKey(nameof(UpdatePropertiesAsync))}", x =>
            {
                return x.Update<RecurringJobPropertyTable>().Table()
                        .Set.Column(c => c.Type).To.Parameter(c => c.Type)
                        .Set.Column(c => c.OriginalType).To.Parameter(c => c.OriginalType)
                        .Set.Column(c => c.TextValue).To.Parameter(c => c.TextValue)
                        .Set.Column(c => c.NumberValue).To.Parameter(c => c.NumberValue)
                        .Set.Column(c => c.FloatingNumberValue).To.Parameter(c => c.FloatingNumberValue)
                        .Set.Column(c => c.BooleanValue).To.Parameter(c => c.BooleanValue)
                        .Set.Column(c => c.DateValue).To.Parameter(c => c.DateValue)
                        .Set.Column(c => c.OtherValue).To.Parameter(c => c.OtherValue)
                        .Set.Column(c => c.ModifiedAt).To.Parameter(c => c.ModifiedAt)
                        .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(recurringJobId))
                                    .And.Column(c => c.Name).EqualTo.Parameter(c => c.Name));
            });
            _logger.Trace($"Updating each property for recurring job <{HiveLog.Job.IdParam}> using query <{query}>", recurringJobId);

            foreach (var property in properties)
            {
                _logger.Debug($"Updating property <{property.Name}> for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
                var parameters = property.ToUpdateParameters(null);
                parameters.AddRecurringJobId(recurringJobId);

                var updated = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

                if (updated != 1) throw new InvalidOperationException($"Property <{property.Name}> for recurring job <{recurringJobId}> was not updated");
                _logger.Debug($"Updated property <{property.Name}> for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
            }

            _logger.Log($"Updated <{properties.Length}> properties for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
        }
        /// <summary>
        /// Deletes properties for recurring job <paramref name="recurringJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="recurringJobId">The id of the job to insert the properties for</param>
        /// <param name="properties">The names of the properties to delete</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task DeletePropertiesAsync(MySqlStorageConnection connection, string recurringJobId, string[] properties, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            recurringJobId.ValidateArgumentNotNullOrWhitespace(nameof(recurringJobId));
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Deleting <{properties.Length}> properties for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(DeletePropertiesAsync)}.{properties.Length}"), x =>
            {
                return x.Delete<RecurringJobPropertyTable>().From()
                        .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(recurringJobId)).And
                                     .Column(c => c.Name).In.Parameters(properties.Select((x, i) => $"{nameof(properties)}{i}")));
            });
            _logger.Trace($"Deleting <{properties.Length}> properties for recurring job <{HiveLog.Job.IdParam}> using query <{query}>", recurringJobId);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(recurringJobId);
            properties.Execute((i, x) => parameters.AddPropertyName(x, $"{nameof(properties)}{i}"));

            var deleted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token));
            if (deleted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be deleted but only <{deleted}> were deleted");
            _logger.Log($"Deleting <{deleted}> properties for recurring job <{HiveLog.Job.IdParam}>", recurringJobId);
        }
        /// <inheritdoc/>
        public virtual async Task CreateRecurringJobActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            action.ValidateArgument(nameof(action));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Creating new action on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateRecurringJobActionAsync)), x =>
            {
                return x.Insert<RecurringJobActionTable>().Into(table: TableNames.BackgroundJobActionTable).Columns(x => x.RecurringJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAt)
                        .Parameters(x => x.RecurringJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAt);
            });
            _logger.Trace($"Creating new action on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", action.ComponentId, connection.Environment);

            // Execute query
            var recurringJobAction = new RecurringJobActionTable(action, _hiveOptions.Get(_environment), _cache);
            var parameters = recurringJobAction.ToCreateParameters();

            _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Created new action on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", action.ComponentId, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<(bool WasLocked, RecurringJobStorageData Data)> TryLockAndTryGetRecurringJobAsync(string id, string requester, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            // Generate query
            _logger.Log($"Trying to set lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{requester}>", id, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryLockAndTryGetRecurringJobAsync)), x =>
            {
                var update = x.Update<RecurringJobTable>().Table()
                              .Set.Column(c => c.LockedBy).To.Parameter(nameof(requester))
                              .Set.Column(c => c.LockedAt).To.CurrentDate(DateType.Utc)
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester))));

                var ifUpdated = x.If().Condition(x => x.RowCount().GreaterThan.Value(0))
                                      .Then(x.Select().Value(1))
                                 .Else
                                      .Then(x.Select().Value(0));

                var select = SelectRecurringJobById(x);

                return x.New().Append(update).Append(ifUpdated).Append(select);
            });
            _logger.Trace($"Trying to set lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{requester}> using query <{query}>", id, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(requester, nameof(requester));
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var wasUpdated = reader.ReadSingle<bool>();
            var recurringJob = ReadRecurringJobs(reader, storageConnection.Environment).FirstOrDefault();
            _logger.Log($"Tried to set lock on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{requester}>", id, connection.Environment);
            return (wasUpdated, recurringJob);
        }
        /// <inheritdoc/>
        public virtual async Task<(bool WasExtended, LockStorageData Data)> TryHeartbeatLockOnRecurringJobAsync(IStorageConnection connection, string id, string holder, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to set lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryHeartbeatLockOnRecurringJobAsync)), x =>
            {
                var update = x.Update<RecurringJobTable>().Table()
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));

                var ifUpdated = x.If().Condition(x => x.RowCount().GreaterThan.Value(0))
                                      .Then(x.Select().Value(1))
                                 .Else
                                      .Then(x.Select().Value(0));

                var select = x.Select<RecurringJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From()
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                return x.New().Append(update).Append(ifUpdated).Append(select);
            });
            _logger.Trace($"Trying to set lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            var wasExtended = await reader.ReadSingleAsync<bool>().ConfigureAwait(false);


            if (wasExtended)
            {
                _logger.Log($"Set lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            }
            else
            {
                _logger.Warning($"Could not set lock heartbeat on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            }

            var job = await reader.ReadSingleOrDefaultAsync<RecurringJobTable>().ConfigureAwait(false);

            if (job.Id.HasValue())
            {
                return (wasExtended, job.ToLockStorageFormat());
            }

            return (wasExtended, null);
        }
        /// <inheritdoc/>
        public virtual async Task<bool> UnlockRecurringJobAsync(IStorageConnection connection, string id, string holder, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to remove lock from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UnlockRecurringJobAsync)), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying remove lock from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed lock from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", id, connection.Environment, holder);
                return true;
            }
            else
            {
                _logger.Warning($"Could not remove lock from background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> for <{holder}>", id, connection.Environment, holder);
                return false;
            }
        }
        /// <inheritdoc/>
        public virtual async Task UnlockRecurringJobsAsync(string[] ids, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Trying to remove locks from <{ids.Length}> recurring jobs in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UnlockRecurringJobsAsync)}.{ids.Length}"), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).In.Parameters(ids.Select((x, i) => $"{nameof(ids)}{i}")).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying to remove locks from <{ids.Length}> recurring jobs in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> using query <{query}>", connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();

            parameters.AddLocker(holder, nameof(holder));
            ids.Execute((i, x) => parameters.AddRecurringJobId(x, $"{nameof(ids)}{i}"));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed locks from <{ids.Length}> recurring jobs of the total <{ids.Length}> in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}>", connection.Environment, holder);
            }
            else
            {
                _logger.Warning($"Could not remove any locks from the <{ids.Length}> recurring jobs in environment <{HiveLog.EnvironmentParam}> for <{holder}>", connection.Environment);
            }
        }
        /// <inheritdoc/>
        public virtual async Task<RecurringJobStorageData[]> SearchRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Selecting the next max <{pageSize}> recurring jobs from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated search query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedRecurringJobSearchQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildRecurringJobSearchQuery(_queryProvider, queryConditions, pageSize, page, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }

            parameters.Add(nameof(pageSize), pageSize);
            parameters.Add(nameof(page), pageSize * (page - 1));

            _logger.Trace($"Selecting the next max <{pageSize}> recurring jobs from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var jobStorageData = ReadRecurringJobs(reader, storageConnection.Environment);

            _logger.Log($"Selected <{jobStorageData.Length}> recurring jobs from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return jobStorageData.ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<long> CountRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Counting the amount of recurring jobs in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated count query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedRecurringJobCountQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildRecurringJobCountQuery(x, queryConditions, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }

            _logger.Trace($"Counting the amount of recurring jobs in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var total = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Counted <{total}> recurring jobs in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return total;
        }
        /// <inheritdoc/>
        public virtual async Task<RecurringJobStorageData[]> LockRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Trying to lock the next <{limit}> recurring jobs in environment <{HiveLog.EnvironmentParam}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated lock query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"{nameof(LockRecurringJobsAsync)}.GeneratedRecurringJobSearchAndLockQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildRecurringJobSearchAndLockQuery(_queryProvider, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }
            parameters.Add(nameof(limit), limit);
            parameters.Add(nameof(requester), requester);

            _logger.Trace($"Selecting the ids of the next <{limit}> recurring jobs in environment <{HiveLog.EnvironmentParam}> to lock for <{requester}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            var ids = await storageConnection.MySqlConnection.QueryAsync<string>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (!ids.HasValue())
            {
                _logger.Log($"No recurring jobs in environment <{HiveLog.EnvironmentParam}> matched the query condition <{queryConditions}> for <{HiveLog.Job.LockHolderParam}>", storageConnection.Environment, requester);
                return Array.Empty<RecurringJobStorageData>();
            }

            // Update matching jobs
            _ = await UpdateRecurringJobLocksByIdsAsync(connection, ids, requester, token).ConfigureAwait(false);

            // Select updated background jobsa
            var jobStorageData = await GetRecurringJobsByIdsAsync(connection, ids, token).ConfigureAwait(false);

            _logger.Log($"Locked <{jobStorageData.Length}> recurring jobs in environment <{HiveLog.EnvironmentParam}> for <{HiveLog.Job.LockHolderParam}> matching the query condition <{queryConditions}>", storageConnection.Environment, requester);
            return jobStorageData.ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<string[]> GetAllRecurringJobQueuesAsync(IStorageConnection connection, string prefix = null, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var filterByPrefix = prefix.HasValue();
            _logger.Log($"Selecting all distinct recurring job queues from environment <{HiveLog.EnvironmentParam}> matching prefix <{prefix}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetAllRecurringJobQueuesAsync)}.{filterByPrefix}"), x =>
            {
                var select = x.Select<RecurringJobTable>()
                            .Distinct().Column(x => x.Queue)
                            .From();
                if (filterByPrefix) select.Where(x => x.Column(x => x.Queue).Like.Concat(StaticSql.Expressions.Parameter(nameof(prefix)), '%'));
                return select;
            });
            _logger.Trace($"Selecting all distinct recurring job queues from environment <{HiveLog.EnvironmentParam}> matching prefix <{prefix}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            if (filterByPrefix) parameters.Add(nameof(prefix), prefix, DbType.String, size: 255);
            var queues = (await storageConnection.MySqlConnection.QueryAsync<string>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Selected <{queues.Length}> distinct recurring job queues from environment <{HiveLog.EnvironmentParam}> matching prefix <{prefix}>", storageConnection.Environment);
            return queues;
        }
        /// <inheritdoc/>
        public virtual async Task<ActionInfo[]> GetNextRecurringJobActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Fetching the next <{limit}> pending actions on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetNextRecurringJobActionsAsync)), x =>
            {
                return x.Select<RecurringJobActionTable>().All().From(TableNames.RecurringJobActionTable, typeof(RecurringJobActionTable))
                       .Where(x => x.Column(x => x.RecurringJobId).EqualTo.Parameter(nameof(id)))
                       .OrderBy(x => x.Priority, SortOrders.Ascending).OrderBy(x => x.CreatedAt, SortOrders.Ascending)
                       .Limit(x => x.Parameter(nameof(limit)));
            });
            _logger.Trace($"Fetching the next <{limit}> pending actions on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id);
            parameters.AddLimit(limit);
            var actions = (await storageConnection.MySqlConnection.QueryAsync<RecurringJobActionTable>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Fetched <{actions?.Length}> pending actions on recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> ", id, connection.Environment);
            return actions.Select(x => x.ToAction(_hiveOptions.Get(_environment), _cache)).ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<bool> DeleteRecurringJobActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var actionId = id.ConvertTo<long>();
            _logger.Log($"Removing recurring job action <{actionId}> in environment <{HiveLog.EnvironmentParam}>", actionId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(DeleteRecurringJobActionByIdAsync)), x =>
            {
                return x.Delete<RecurringJobActionTable>().From(TableNames.RecurringJobActionTable, typeof(RecurringJobActionTable))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(actionId)));
            });
            _logger.Trace($"Removing recurring job action <{actionId}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", actionId, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(actionId), actionId);

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Removing of recurring job action <{actionId}> in environment <{HiveLog.EnvironmentParam}> was <{wasDeleted}>", actionId, connection.Environment);
            return wasDeleted;
        }
        /// <inheritdoc/>
        public virtual async Task CreateRecurringJobLogsAsync(IStorageConnection connection, string id, IEnumerable<LogEntry> logEntries, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            logEntries.ValidateArgumentNotNullOrEmpty(nameof(logEntries));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var count = logEntries.GetCount();
            _logger.Log($"Inserting <{count}> log entries for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(CreateRecurringJobLogsAsync)}.{count}"), x =>
            {
                var insertQuery = x.Insert<RecurringJobLogTable>().Into().ColumnsOf(nameof(RecurringJobLogTable.CreatedAtUtc));
                logEntries.Execute((i, x) =>
                {
                    insertQuery.Values(x => x.Parameter(p => p.RecurringJobId, i.ToString())
                                      , x => x.Parameter(p => p.LogLevel, i.ToString())
                                      , x => x.Parameter(p => p.Message, i.ToString())
                                      , x => x.Parameter(p => p.ExceptionType, i.ToString())
                                      , x => x.Parameter(p => p.ExceptionMessage, i.ToString())
                                      , x => x.Parameter(p => p.ExceptionStackTrace, i.ToString())
                                      , x => x.Parameter(p => p.CreatedAt, i.ToString()));
                });
                return insertQuery;
            });
            _logger.Trace($"Inserting <{count}> log entries for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            logEntries.Execute((i, x) => new RecurringJobLogTable(id, x).AppendCreateParameters(parameters, i));

            var inserted = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Inserted <{inserted}> log entries for recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<LogEntry[]> GetRecurringJobLogsAsync(IStorageConnection connection, string id, LogLevel[] logLevels, int page, int pageSize, bool mostRecentFirst, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            page.ValidateArgumentLarger(nameof(page), 0);
            pageSize.ValidateArgumentLarger(nameof(pageSize), 1);
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Fetching up to <{pageSize}> logs from page <{page}> of recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetRecurringJobLogsAsync)}.{logLevels?.Length ?? 0}.{mostRecentFirst}"), x =>
            {
                var getQuery = x.Select<RecurringJobLogTable>().All()
                                .From()
                                .Limit(SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(page)), SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(pageSize)))
                                .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(id)))
                                .OrderBy(x => x.CreatedAt, mostRecentFirst ? SortOrders.Descending : SortOrders.Ascending);

                if (logLevels.HasValue()) getQuery.Where(x => x.Column(x => x.LogLevel).In.Parameters(logLevels.Select((i, x) => $"{nameof(logLevels)}{i}")));
                return getQuery;
            });
            _logger.Trace($"Fetching up to <{pageSize}> logs from page <{page}> of recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id);
            parameters.AddPage(page, pageSize);
            parameters.AddPageSize(pageSize);
            if (logLevels.HasValue()) logLevels.Execute((i, x) => parameters.Add($"{nameof(logLevels)}{i}", x, DbType.Int32, ParameterDirection.Input));

            var logs = (await storageConnection.MySqlConnection.QueryAsync<LogEntry>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Fetched <{logs.Length}> logs from page <{page}> of recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            return logs;
        }
        /// <inheritdoc/>
        public virtual async Task<(bool Exists, string Data)> TryGetRecurringJobDataAsync(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to get data <{name}> from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryGetRecurringJobDataAsync)), x =>
            {
                return x.Select<RecurringJobDataTable>().Column(c => c.Value)
                        .From()
                        .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(id))
                                 .And.Column(c => c.Name).EqualTo.Parameter(nameof(name)));
            });
            _logger.Trace($"Trying to get data <{name}> from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddDataName(name);

            var value = await storageConnection.MySqlConnection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Fetched data <{name}> from recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>: {value ?? "NULL"}", id, storageConnection.Environment);
            return (value != null, value);
        }
        /// <inheritdoc/>
        public virtual async Task SetRecurringJobDataAsync(IStorageConnection connection, string id, string name, string value, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Saving data <{name}> to recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(SetRecurringJobDataAsync)), b =>
            {
                var updateQuery = b.Update<RecurringJobDataTable>().Table()
                                   .Set.Column(c => c.Value).To.Parameter(p => p.Value)
                                   .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(p => p.RecurringJobId)
                                            .And.Column(c => c.Name).EqualTo.Parameter(p => p.Name));

                // Insert if update did not update anything
                var insertQuery = b.If().Condition(x => x.RowCount().EqualTo.Value(0))
                                        .Then(x => x.Append(b.Insert<RecurringJobDataTable>().Into().Columns(c => c.RecurringJobId, c => c.Name, c => c.Value)
                                                             .Parameters(p => p.RecurringJobId, p => p.Name, p => p.Value)
                                                           )
                                        );

                return b.New().Append(updateQuery).Append(insertQuery);
            });
            _logger.Trace($"Saving data <{name}> to recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var table = new RecurringJobDataTable()
            {
                RecurringJobId = id,
                Name = name,
                Value = value
            };
            var parameters = table.ToCreateParameters();
            await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to recurring job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>", id, storageConnection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<RecurringJobStorageData[]> GetTimedOutRecurringJobs(IStorageConnection connection, int limit, string requester, TimeSpan timeoutThreshold, CancellationToken token = default)
        {
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            // Get ids to update
            _logger.Log($"Selecting at most <{limit}> recurring jobs where the lock timed out in environment <{HiveLog.EnvironmentParam}> for <{requester}> with update lock", connection.Environment);
            var selectIdQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetTimedOutRecurringJobs)}.Select"), x =>
            {
                return x.Select<RecurringJobTable>().From(table: TableNames.RecurringJobTable, typeof(RecurringJobTable))
                        .Column(x => x.Id)
                        .Where(x => x.Column(x => x.LockedBy).IsNotNull.And
                                     .Column(x => x.LockHeartbeat).LesserThan.ModifyDate(x => x.CurrentDate(DateType.Utc), x => x.Parameter(nameof(timeoutThreshold)), DateInterval.Millisecond)
                              )
                        .ForUpdateSkipLocked()
                        .Limit(x => x.Parameter(nameof(limit)));
            });
            _logger.Trace($"Selecting at most <{limit}> recurring jobs where the lock timed out in environment <{HiveLog.EnvironmentParam}> for <{requester}> with update lock using query <{selectIdQuery}>", connection.Environment);

            var parameters = new DynamicParameters();
            parameters.AddLimit(limit);
            parameters.Add(nameof(timeoutThreshold), -timeoutThreshold.TotalMilliseconds, DbType.Double, ParameterDirection.Input);
            var ids = (await storageConnection.MySqlConnection.QueryAsync<string>(new CommandDefinition(selectIdQuery, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            if (!ids.HasValue())
            {
                _logger.Log($"No timed out recurring jobs in environment <{HiveLog.EnvironmentParam}> locked for <{requester}>", connection.Environment);
                return Array.Empty<RecurringJobStorageData>();
            }

            // Update new lock owner
            _ = await UpdateRecurringJobLocksByIdsAsync(connection, ids, requester, token).ConfigureAwait(false);

            // Select background jobs
            return await GetRecurringJobsByIdsAsync(connection, ids, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public virtual async Task<(int StatesRemoved, int LogsRemoved)> ApplyRetention(IStorageConnection connection, string id, RecurringJobRetentionMode stateRetentionMode, int stateRetentionAmount, RecurringJobRetentionMode logRetentionMode, int logRetentionAmount, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection, true);
            id = Guard.IsNotNullOrWhitespace(id);
            stateRetentionAmount = Guard.IsLargerOrEqual(stateRetentionAmount, 1);
            logRetentionAmount = Guard.IsLargerOrEqual(logRetentionAmount, 1);

            if (stateRetentionMode == RecurringJobRetentionMode.KeepAll && logRetentionMode == RecurringJobRetentionMode.KeepAll) return (0, 0);

            _logger.Log($"Applying state retention mode <{stateRetentionMode}> with amount <{stateRetentionAmount}> and log retention mode <{logRetentionMode}> with amount <{logRetentionAmount}> to recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);

            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(ApplyRetention)}.{stateRetentionMode}.{logRetentionMode}"), b =>
            {
                var statement = b.New();
                switch (stateRetentionMode)
                {
                    case RecurringJobRetentionMode.KeepAll:
                        break;
                    case RecurringJobRetentionMode.OlderThan:
                        var deleteStateOlderThanQuery = b.Delete<RecurringJobStateTable>().From(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable))
                                                         .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(id))
                                                                      .And.Column(c => c.ElectedDate).LesserThan.ModifyDate(x => x.CurrentDate(DateType.Utc), x => x.Expressions(null, x => x.Expression("-"), x => x.Parameter(nameof(stateRetentionAmount))), DateInterval.Day));
                        statement.Append(deleteStateOlderThanQuery);
                        statement.Append(b.Select().ColumnExpression(x => x.RowCount()));
                        break;
                    case RecurringJobRetentionMode.Amount:
                        var deleteStateAmountQuery = b.Delete<RecurringJobStateTable>().From(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable))
                                                      .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(id)).And.Column(x => x.Id).In.Query(b.Select<RecurringJobStateTable>().Column(x => x.Id)
                                                                                                                                                                .FromQuery<RecurringJobStateTable>(b.Select<RecurringJobStateTable>().Column(x => x.Id)
                                                                                                                                                                                                    .From(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable))
                                                                                                                                                                                                    .OrderBy(x => x.ElectedDate, SortOrders.Descending)
                                                                                                                                                                                                    .Limit(x => x.Parameter(nameof(stateRetentionAmount)), x => x.Value(int.MaxValue))
                                                                                                                                                                                                  )
                                                                                                                                                              )
                                                            );
                        statement.Append(deleteStateAmountQuery);
                        statement.Append(b.Select().ColumnExpression(x => x.RowCount()));
                        break;
                    default: throw new NotSupportedException($"State retention mode <{stateRetentionMode}> is not supported");
                }

                switch (logRetentionMode)
                {
                    case RecurringJobRetentionMode.KeepAll:
                        break;
                    case RecurringJobRetentionMode.OlderThan:
                        var deleteLogOlderThanQuery = b.Delete<RecurringJobLogTable>().From(TableNames.RecurringJobLogTable, typeof(RecurringJobLogTable))
                                                       .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(id))
                                                                    .And.Column(c => c.CreatedAt).LesserThan.ModifyDate(x => x.CurrentDate(DateType.Utc), x => x.Expressions(null, x => x.Expression("-"), x => x.Parameter(nameof(logRetentionAmount))), DateInterval.Day));
                        statement.Append(deleteLogOlderThanQuery);
                        statement.Append(b.Select().ColumnExpression(x => x.RowCount()));
                        break;
                    case RecurringJobRetentionMode.Amount:
                        var deleteLogAmountQuery = b.Delete<RecurringJobLogTable>().From(TableNames.RecurringJobLogTable, typeof(RecurringJobLogTable))
                                                    .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(id)).And.Column(x => x.Id).In.Query(b.Select<RecurringJobLogTable>().Column(x => x.Id)
                                                                                                                                                              .FromQuery<RecurringJobLogTable>(b.Select<RecurringJobLogTable>().Column(x => x.Id)
                                                                                                                                                                                                .From(TableNames.RecurringJobLogTable, typeof(RecurringJobLogTable))
                                                                                                                                                                                                .OrderBy(x => x.CreatedAt, SortOrders.Descending)
                                                                                                                                                                                                .Limit(x => x.Parameter(nameof(logRetentionAmount)), x => x.Value(int.MaxValue))
                                                                                                                                                                                               )
                                                                                                                                                             )
                                                          );
                        statement.Append(deleteLogAmountQuery);
                        statement.Append(b.Select().ColumnExpression(x => x.RowCount()));
                        break;
                    default: throw new NotSupportedException($"Log retention mode <{logRetentionMode}> is not supported");
                }
                return statement;
            });

            _logger.Trace($"Applying state retention mode <{stateRetentionMode}> with amount <{stateRetentionAmount}> and log retention mode <{logRetentionMode}> with amount <{logRetentionAmount}> to recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.Add(nameof(stateRetentionAmount), stateRetentionAmount, DbType.Int32);
            parameters.Add(nameof(logRetentionAmount), logRetentionAmount, DbType.Int32);
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var deletedStates = 0;
            if (stateRetentionMode != RecurringJobRetentionMode.KeepAll)
            {
                deletedStates = await reader.ReadFirstAsync<int>().ConfigureAwait(false);
            }
            var deletedLogs = 0;
            if (logRetentionMode != RecurringJobRetentionMode.KeepAll)
            {
                deletedLogs = await reader.ReadFirstAsync<int>().ConfigureAwait(false);
            }

            _logger.Log($"Removed <{deletedStates}> older states and <{deletedLogs}> older logs from recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            return (deletedStates, deletedLogs);
        }
        private async Task<int> UpdateRecurringJobLocksByIdsAsync(IStorageConnection connection, IEnumerable<string> ids, string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));

            // Generate query
            _logger.Log($"Updating <{ids.GetCount()}> recurring jobs locks by id in environment <{HiveLog.EnvironmentParam}> so they are held by <{holder}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UpdateRecurringJobLocksByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(x => x.LockedBy).To.Parameter(nameof(holder))
                        .Set.Column(x => x.LockHeartbeat).To.CurrentDate(DateType.Utc)
                        .Set.Column(x => x.LockedAt).To.CurrentDate(DateType.Utc)
                        .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")));
            });
            _logger.Trace($"Updating <{ids.GetCount()}> recurring jobs locks by id in environment <{HiveLog.EnvironmentParam}> so they are held by <{holder}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddLocker(holder, nameof(holder));
            ids.Execute((i, x) => parameters.AddRecurringJobId(x, $"Id{i}"));

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Updated <{updated}> recurring jobs locks by id in environment <{HiveLog.EnvironmentParam}> so they are now held by <{HiveLog.Job.LockHolderParam}>", storageConnection.Environment, holder);
            return updated;
        }

        private async Task<RecurringJobStorageData[]> GetRecurringJobsByIdsAsync(IStorageConnection connection, IEnumerable<string> ids, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));

            _logger.Log($"Selecting <{ids.GetCount()}> recurring jobs by id in environment <{HiveLog.EnvironmentParam}>", storageConnection.Environment);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetRecurringJobsByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return SelectRecurringJob(x)
                       .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")));
            });
            _logger.Trace($"Selecting <{ids.GetCount()}> background jobs by id in environment <{HiveLog.EnvironmentParam}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            ids.Execute((i, x) => parameters.AddRecurringJobId(x, $"Id{i}"));

            return await QueryRecurringJobs(storageConnection, new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a query builder that contains a select statement for recurring jobs fetched by cte.
        /// </summary>
        /// <param name="provider">The provider to use to build the query</param>
        /// <returns>A select statement for a recuring job</returns>
        protected ISelectStatementBuilder<RecurringJobTable> SelectRecurringJobByCte(ISqlQueryProvider provider)
            => SelectRecurringJob(provider)
               .InnerJoin().Table("cte", 'c').On(x => x.Column(x => x.Id).EqualTo.Column('c', x => x.Id));
        private string BuildRecurringJobSearchAndLockQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            bool joinProperty = false;
            bool joinState = false;

            // Select the ids to update because MariaDB update refuses to use the same index as selects and it rather wants to scan the whole table
            var selectIdQuery = queryProvider.Select<RecurringJobTable>().From().Column(x => x.Id).ForUpdateSkipLocked()
                                             .Where(x =>
                                             {
                                                 var builder = x.WhereGroup(x =>
                                                 {
                                                     (joinProperty, joinState) = BuildWhereStatement<RecurringJobTable, string, RecurringJobPropertyTable, RecurringJobStateTable>(x, parameters, queryConditions.Conditions, nameof(RecurringJobPropertyTable.RecurringJobId));
                                                     return x.LastBuilder;
                                                 });

                                                 if (allowAlreadyLocked)
                                                 {
                                                     return builder.And.WhereGroup(x => x.Column(x => x.LockedBy).IsNull
                                                                                         .Or.Column(x => x.LockedBy).EqualTo.Parameter(nameof(requester)));
                                                 }
                                                 else
                                                 {
                                                     return builder.And.Column(x => x.LockedBy).IsNull;
                                                 }
                                             })
                                             .Limit(x => x.Parameter(nameof(limit)));

            // Join if needed
            if (joinProperty) selectIdQuery.LeftJoin().Table(TableNames.RecurringJobPropertyTable, typeof(RecurringJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobPropertyTable>(x => x.RecurringJobId));
            if (joinState) selectIdQuery.InnerJoin().Table(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobStateTable>(x => x.RecurringJobId));

            if (orderBy.HasValue)
            {
                QueryRecurringJobOrderByTarget orderByTarget = orderBy.Value;
                switch (orderByTarget)
                {
                    case QueryRecurringJobOrderByTarget.Id:
                        selectIdQuery.OrderBy(x => x.Id, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.Queue:
                        selectIdQuery.OrderBy(x => x.Queue, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.Priority:
                        selectIdQuery.OrderBy(x => x.Priority, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.CreatedAt:
                        selectIdQuery.OrderBy(x => x.CreatedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.ModifiedAt:
                        selectIdQuery.OrderBy(x => x.ModifiedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.ExpectedExecutionDate:
                        selectIdQuery.OrderBy(x => x.ExpectedExecutionDate, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.LastStartedDate:
                        selectIdQuery.OrderBy(x => x.LastStartedDate, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.LastCompletedDate:
                        selectIdQuery.OrderBy(x => x.LastCompletedDate, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    default: throw new NotSupportedException($"Order by target <{orderByTarget}> is not supported");
                }
            }

            return selectIdQuery.Build(_compileOptions);
        }
        private string BuildRecurringJobSearchQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int pageSize, int page, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            bool joinProperty = false;
            bool joinState = false;

            // Select id of matching
            var selectIdQuery = queryProvider.Select<RecurringJobTable>().From().Column(x => x.Id)
                                             .Where(x =>
                                             {
                                                 (joinProperty, joinState) = BuildWhereStatement<RecurringJobTable, string, RecurringJobPropertyTable, RecurringJobStateTable>(x, parameters, queryConditions.Conditions, nameof(RecurringJobPropertyTable.RecurringJobId));
                                                 return x.LastBuilder;
                                             })
                                             .Limit(x => x.Parameter(nameof(page)), x => x.Parameter(nameof(pageSize)));

            // Join if needed
            if (joinProperty) selectIdQuery.LeftJoin().Table(TableNames.RecurringJobPropertyTable, typeof(RecurringJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobPropertyTable>(x => x.RecurringJobId));
            if (joinState) selectIdQuery.InnerJoin().Table(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobStateTable>(x => x.RecurringJobId));

            if (orderBy.HasValue)
            {
                QueryRecurringJobOrderByTarget orderByTarget = orderBy.Value;
                switch (orderByTarget)
                {
                    case QueryRecurringJobOrderByTarget.Id:
                        selectIdQuery.OrderBy(x => x.Id, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.Queue:
                        selectIdQuery.OrderBy(x => x.Queue, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.Priority:
                        selectIdQuery.OrderBy(x => x.Priority, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.CreatedAt:
                        selectIdQuery.OrderBy(x => x.CreatedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.ModifiedAt:
                        selectIdQuery.OrderBy(x => x.ModifiedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.ExpectedExecutionDate:
                        selectIdQuery.OrderBy(x => x.ExpectedExecutionDate, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.LastStartedDate:
                        selectIdQuery.OrderBy(x => x.LastStartedDate, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryRecurringJobOrderByTarget.LastCompletedDate:
                        selectIdQuery.OrderBy(x => x.LastCompletedDate, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    default: throw new NotSupportedException($"Order by target <{orderByTarget}> is not supported");
                }
            }

            // Select recurring jobs
            var selectQuery = queryProvider.With().Cte("cte")
                                                        .As(selectIdQuery)
                                                   .Execute(SelectRecurringJobByCte(queryProvider));

            return selectQuery.Build(_compileOptions);
        }
        private string BuildRecurringJobCountQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            bool joinProperty = false;
            bool joinState = false;

            var countQuery = queryProvider.Select<RecurringJobTable>().From();
            countQuery.Where(x =>
            {
                (joinProperty, joinState) = BuildWhereStatement<RecurringJobTable, string, RecurringJobPropertyTable, RecurringJobStateTable>(x, parameters, queryConditions.Conditions, nameof(RecurringJobPropertyTable.RecurringJobId));
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.LeftJoin().Table(TableNames.RecurringJobPropertyTable, typeof(RecurringJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobPropertyTable>(x => x.RecurringJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobStateTable>(x => x.RecurringJobId));

            // Count total matching
            countQuery.Count(x => x.Id);

            return countQuery.Build(_compileOptions);
        }
        #endregion

        #region Colony
        private IQueryBuilder CreateSyncDaemonQuery(ISqlQueryProvider queryBuilder, int amountOfDeamons, string colonyIdParameterName, string parameterSuffix)
        {
            queryBuilder = Guard.IsNotNull(queryBuilder);
            amountOfDeamons = Guard.IsLargerOrEqual(amountOfDeamons, 1);
            colonyIdParameterName = Guard.IsNotNullOrWhitespace(colonyIdParameterName);
            parameterSuffix = Guard.IsNotNullOrWhitespace(parameterSuffix);

            var syncQuery = queryBuilder.New();

            // Insert or update 
            var insertOrUpdate = queryBuilder.Insert<ColonyDaemonTable>().Into()
                                             .Columns(x => x.ColonyId,
                                                      x => x.Name,
                                                      x => x.Priority,
                                                      x => x.OriginalInstanceTypeName,
                                                      x => x.Status,
                                                      x => x.StateTypeName,
                                                      x => x.StateStorageValue,
                                                      x => x.RestartPolicy,
                                                      x => x.EnabledLogLevel,
                                                      x => x.CreatedAt,
                                                      x => x.ModifiedAt)
                                             .OnDuplicateKeyUpdate(0, 1, 9);
            for (int i = 0; i < amountOfDeamons; i++)
            {
                insertOrUpdate.Values(x => x.Parameter(x => x.ColonyId, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.Name, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.Priority, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.OriginalInstanceTypeName, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.Status, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.StateTypeName, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.StateStorageValue, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.RestartPolicy, $"{parameterSuffix}{i}"),
                                      x => x.Parameter(x => x.EnabledLogLevel, $"{parameterSuffix}{i}"),
                                      x => x.CurrentDate(DateType.Utc),
                                      x => x.CurrentDate(DateType.Utc));

            }

            // Delete all daemons not included in colony
            var delete = queryBuilder.Delete<ColonyDaemonTable>().From()
                                     .Where(x => x.Column(c => c.ColonyId).EqualTo.Parameter(colonyIdParameterName).And
                                                  .Column(c => c.Name).NotIn.Parameters(Enumerable.Range(0, amountOfDeamons).Select(x => $"{nameof(ColonyDaemonTable.Name)}{parameterSuffix}{x}")));

            return syncQuery.Append(insertOrUpdate).Append(delete);
        }

        private IQueryBuilder CreateSyncColonyPropertiesQuery(ISqlQueryProvider queryBuilder, int amountOfProperties, string parameterSuffix, bool removeNotPresent)
        {
            queryBuilder = Guard.IsNotNull(queryBuilder);
            amountOfProperties = Guard.IsLargerOrEqual(amountOfProperties, 0);
            parameterSuffix = Guard.IsNotNullOrWhitespace(parameterSuffix);
            Guard.Is(() => amountOfProperties > 0 || removeNotPresent, () => new ArgumentException($"Cannot create sync query when there is nothing to insert and nothing to remove"));

            var syncQuery = queryBuilder.New();

            if (amountOfProperties > 0)
            {
                // Insert or update
                var insertOrUpdate = queryBuilder.Insert<ColonyPropertyTable>().Into()
                                                 .Columns(x => x.ColonyId,
                                                          x => x.Name,
                                                          x => x.Type,
                                                          x => x.OriginalType,
                                                          x => x.TextValue,
                                                          x => x.NumberValue,
                                                          x => x.FloatingNumberValue,
                                                          x => x.DateValue,
                                                          x => x.BooleanValue,
                                                          x => x.OtherValue,
                                                          x => x.CreatedAt,
                                                          x => x.ModifiedAt)
                                                 .OnDuplicateKeyUpdate(0, 1, 10);

                for (int i = 0; i < amountOfProperties; i++)
                {
                    insertOrUpdate.Values(x => x.Parameter(x => x.ColonyId, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.Name, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.Type, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.OriginalType, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.TextValue, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.NumberValue, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.FloatingNumberValue, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.DateValue, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.BooleanValue, $"{parameterSuffix}{i}"),
                                          x => x.Parameter(x => x.OtherValue, $"{parameterSuffix}{i}"),
                                          x => x.CurrentDate(DateType.Utc),
                                          x => x.CurrentDate(DateType.Utc));

                }
                syncQuery.Append(insertOrUpdate);

                if (removeNotPresent)
                {
                    // Delete all properties not included in colony
                    var delete = queryBuilder.Delete<ColonyPropertyTable>().From()
                                             .Where(x => x.Column(c => c.ColonyId).EqualTo.Parameter(c => c.ColonyId).And
                                                          .Column(c => c.Name).NotIn.Parameters(Enumerable.Range(0, amountOfProperties).Select(x => $"{nameof(ColonyPropertyTable.Name)}{parameterSuffix}{x}")));
                    syncQuery.Append(delete);
                }
            }
            else if (removeNotPresent)
            {
                // Remove all present properties
                var delete = queryBuilder.Delete<ColonyPropertyTable>().From()
                                         .Where(x => x.Column(c => c.ColonyId).EqualTo.Parameter(c => c.ColonyId));
                syncQuery.Append(delete);
            }

            return syncQuery;
        }

        private IQueryBuilder CreateSyncColonyDaemonPropertiesQuery(ISqlQueryProvider queryBuilder, IReadOnlyCollection<int> propertiesPerDaemonGroup, string colonyIdParameterName, string daemonParameterSuffix, string parameterSuffix, bool removeNotPresent)
        {
            queryBuilder = Guard.IsNotNull(queryBuilder);
            propertiesPerDaemonGroup = Guard.Is(propertiesPerDaemonGroup, x => (x.HasValue() && x.Any(x => x > 0)) || removeNotPresent);
            colonyIdParameterName = Guard.IsNotNullOrWhitespace(colonyIdParameterName);
            daemonParameterSuffix = Guard.IsNotNullOrWhitespace(daemonParameterSuffix);
            parameterSuffix = Guard.IsNotNullOrWhitespace(parameterSuffix);

            var amountOfGroups = propertiesPerDaemonGroup.Count;
            var syncQuery = queryBuilder.New();

            if (propertiesPerDaemonGroup.HasValue() && propertiesPerDaemonGroup.Any(x => x > 0))
            {
                // Insert or update
                var insertOrUpdate = queryBuilder.Insert<ColonyDaemonPropertyTable>().Into()
                                                 .Columns(x => x.ColonyId,
                                                          x => x.DaemonName,
                                                          x => x.Name,
                                                          x => x.Type,
                                                          x => x.OriginalType,
                                                          x => x.TextValue,
                                                          x => x.NumberValue,
                                                          x => x.FloatingNumberValue,
                                                          x => x.DateValue,
                                                          x => x.BooleanValue,
                                                          x => x.OtherValue,
                                                          x => x.CreatedAt,
                                                          x => x.ModifiedAt)
                                                 .OnDuplicateKeyUpdate(0, 1, 2, 11);

                for (int i = 0; i < propertiesPerDaemonGroup.Count; i++)
                {
                    var amountOfProperties = propertiesPerDaemonGroup.GetAtIndex(i);

                    if (amountOfProperties > 0)
                    {
                        // Insert or update
                        for (int y = 0; y < amountOfProperties; y++)
                        {
                            insertOrUpdate.Values(x => x.Parameter(x => x.ColonyId, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.DaemonName, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.Name, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.Type, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.OriginalType, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.TextValue, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.NumberValue, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.FloatingNumberValue, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.DateValue, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.BooleanValue, $"{parameterSuffix}{i}{y}"),
                                                  x => x.Parameter(x => x.OtherValue, $"{parameterSuffix}{i}{y}"),
                                                  x => x.CurrentDate(DateType.Utc),
                                                  x => x.CurrentDate(DateType.Utc));

                        }
                    }
                }

                syncQuery.Append(insertOrUpdate);

                if (removeNotPresent)
                {
                    var delete = queryBuilder.Delete<ColonyDaemonPropertyTable>().From()
                                             .Where(x =>
                                             {
                                                 var w = x.Column(c => c.ColonyId).EqualTo.Parameter(colonyIdParameterName).And;

                                                 for (int i = 0; i < propertiesPerDaemonGroup.Count; i++)
                                                 {
                                                     var index = i;
                                                     var wc = w.WhereGroup(g =>
                                                     {
                                                         var cg = g.Column(c => c.DaemonName).EqualTo.Parameter<ColonyDaemonTable>(c => c.Name, $"{daemonParameterSuffix}{index}");
                                                         var amountOfProperties = propertiesPerDaemonGroup.GetAtIndex(index);
                                                         if (amountOfProperties > 0) cg = cg.And.Column(c => c.Name).NotIn.Parameters(Enumerable.Range(0, amountOfProperties).Select(x => $"{nameof(ColonyDaemonPropertyTable.Name)}{parameterSuffix}{index}{x}"));
                                                         return cg;
                                                     });

                                                     if (index < propertiesPerDaemonGroup.Count - 1) w = wc.Or;
                                                     else return wc;
                                                 }

                                                 return w.LastBuilder;
                                             });
                    syncQuery.Append(delete);
                }
            }
            else if (removeNotPresent)
            {
                // Remove all present properties
                var delete = queryBuilder.Delete<ColonyDaemonPropertyTable>().From()
                                         .Where(x => x.Column(c => c.ColonyId).EqualTo.Parameter(c => c.ColonyId));
                syncQuery.Append(delete);
            }

            return syncQuery;
        }

        private ISelectStatementBuilder<ColonyTable> SelectColonies(ISqlQueryProvider queryBuilder)
        {
            queryBuilder = Guard.IsNotNull(queryBuilder);

            return queryBuilder.Select<ColonyTable>()
                                .AllOf<ColonyTable>()
                                .AllOf<ColonyPropertyTable>()
                                .AllOf<ColonyDaemonTable>()
                                .AllOf<ColonyDaemonPropertyTable>()
                                .LeftJoin().Table<ColonyPropertyTable>().On(x => x.Column<ColonyPropertyTable>(c => c.ColonyId).EqualTo.Column(c => c.Id))
                                .LeftJoin().Table<ColonyDaemonTable>().On(x => x.Column<ColonyDaemonTable>(c => c.ColonyId).EqualTo.Column(c => c.Id))
                                .LeftJoin().Table<ColonyDaemonPropertyTable>().On(x => x.Column<ColonyDaemonPropertyTable>(c => c.ColonyId).EqualTo.Column<ColonyDaemonTable>(c => c.ColonyId).And.Column<ColonyDaemonPropertyTable>(c => c.DaemonName).EqualTo.Column<ColonyDaemonTable>(c => c.Name))
                                .From()
                                .OrderBy<ColonyDaemonTable>(c => c.Priority);
        }

        /// <inheritdoc/>
        public virtual async Task<(bool WasLocked, LockStorageData Lock)> TrySyncAndGetProcessLockOnColonyAsync(IStorageConnection connection, ColonyStorageData colony, string requester, TimeSpan timeout, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection, true);
            colony = Guard.IsNotNull(colony);
            requester = Guard.IsNotNullOrWhitespace(requester);
            timeout = Guard.IsLarger(timeout, TimeSpan.Zero);

            return await TrySyncAndGetProcessLockOnColonyAsync(storageConnection, colony, requester, (TimeSpan?)timeout, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public virtual async Task<(bool HeartbeatWasSet, LockStorageData Lock)> TryHeartbeatProcessLockOnColonyAsync(IStorageConnection connection, string colonyId, string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection, true);
            colonyId = Guard.IsNotNullOrWhitespace(colonyId);
            holder = Guard.IsNotNullOrWhitespace(holder);

            // Generate query
            _logger.Log($"Trying to set the lock heartbeat on colony <{HiveLog.Colony.IdParam}> if it is still held by <{HiveLog.Colony.HolderParam}>", colonyId, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryHeartbeatProcessLockOnColonyAsync)), x =>
            {
                var update = x.Update<ColonyTable>().Table()
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(colonyId))
                                           .And.WhereGroup(x => x.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));

                var ifUpdated = x.If().Condition(x => x.RowCount().GreaterThan.Value(0))
                                      .Then(x.Select().Value(1))
                                 .Else
                                      .Then(x.Select().Value(0));

                var select = x.Select<ColonyTable>()
                              .Column(c => c.Id)
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From()
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(colonyId)));

                return x.New().Append(update).Append(ifUpdated).Append(select);
            });
            _logger.Trace($"Trying to set the lock heartbeat on colony <{HiveLog.Colony.IdParam}> if it is still held by <{HiveLog.Colony.HolderParam}> using query <{query}>", colonyId, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddColonyId(colonyId, nameof(colonyId));
            parameters.AddLocker(holder, nameof(holder));

            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var wasExtended = await reader.ReadSingleAsync<bool>().ConfigureAwait(false);

            if (wasExtended)
            {
                _logger.Log($"Set lock heartbeat on colony <{HiveLog.Colony.IdParam}> for <{HiveLog.Colony.HolderParam}>", colonyId, holder);
            }
            else
            {
                _logger.Warning($"Could not extend lock heartbeat on colony <{HiveLog.Colony.IdParam}> for <{HiveLog.Colony.HolderParam}>", colonyId, holder);
            }

            var colony = await reader.ReadSingleOrDefaultAsync<ColonyTable>().ConfigureAwait(false);

            if (colony?.Id.HasValue() ?? false)
            {
                return (wasExtended, colony.ToLockStorageFormat());
            }

            return (wasExtended, null);
        }
        /// <inheritdoc/>
        public virtual async Task<bool> TrySyncColonyAsync(IStorageConnection connection, ColonyStorageData colony, [Traceable("HiveMind.Colony.Holder", null)] string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection, true);
            colony = Guard.IsNotNull(colony);
            holder = Guard.IsNotNullOrWhitespace(holder);

            var (wasSynced, _) = await TrySyncAndGetProcessLockOnColonyAsync(storageConnection, colony, holder, null, token).ConfigureAwait(false);

            if (wasSynced)
            {
                _logger.Log($"Persisted colony state for <{HiveLog.Colony.HolderParam}>. Persisting daemon logs if any are presents", holder);

                if (colony.Daemons.HasValue() && colony.Daemons.Any(x => x.NewLogEntries.HasValue()))
                {
                    // Create query
                    var amountOfLogs = colony.Daemons.Select(x => x.NewLogEntries?.Count ?? 0).Sum();
                    var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TrySyncColonyAsync)}.Logs.{amountOfLogs}"), x =>
                    {
                        var insertQuery = x.Insert<ColonyDaemonLogTable>().Into().ColumnsOf(nameof(ColonyDaemonLogTable.CreatedAtUtc));
                        Enumerable.Range(0, amountOfLogs).Execute((i, x) =>
                        {
                            insertQuery.Values(x => x.Parameter(p => p.ColonyId, i.ToString())
                                              , x => x.Parameter(p => p.Name, i.ToString())
                                              , x => x.Parameter(p => p.LogLevel, i.ToString())
                                              , x => x.Parameter(p => p.Message, i.ToString())
                                              , x => x.Parameter(p => p.ExceptionType, i.ToString())
                                              , x => x.Parameter(p => p.ExceptionMessage, i.ToString())
                                              , x => x.Parameter(p => p.ExceptionStackTrace, i.ToString())
                                              , x => x.Parameter(p => p.CreatedAt, i.ToString()));
                        });
                        return insertQuery;
                    });
                    _logger.Trace($"Persisting daemon logs for <{HiveLog.Colony.HolderParam}> using query <{query}>", holder);

                    // Execute query
                    var parameters = new DynamicParameters();
                    var counter = 0;
                    colony.Daemons.Where(x => x.NewLogEntries.HasValue()).Execute(d => d.NewLogEntries.Execute(l => new ColonyDaemonLogTable(colony.Id, d.Name, l).AppendCreateParameters(parameters, counter++)));

                    await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

                    _logger.Log($"Persisted <{counter}> daemon logs for <{HiveLog.Colony.HolderParam}> using query <{query}>", holder);
                }
                else
                {
                    _logger.Debug("No daemon logs to persist");
                }
            }
            else
            {
                _logger.Warning($"Could not sync colony state for <{holder}>");
            }

            return wasSynced;
        }
        /// <inheritdoc/>
        public virtual async Task<bool> ReleaseLockOnColonyIfHeldByAsync(IStorageConnection connection, string colonyId, string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection, true);
            colonyId = Guard.IsNotNullOrWhitespace(colonyId);
            holder = Guard.IsNotNullOrWhitespace(holder);

            // Generate query
            _logger.Log($"Trying to release process lock on colony <{HiveLog.Colony.IdParam}> if it is still held by <{HiveLog.Colony.HolderParam}>", colonyId, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(ReleaseLockOnColonyIfHeldByAsync)), x =>
            {
                return x.Update<ColonyTable>().Table()
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(colonyId))
                                    .And.WhereGroup(x => x.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));
            });
            _logger.Trace($"Trying to release process lock on colony <{HiveLog.Colony.IdParam}> if it is still held by <{HiveLog.Colony.HolderParam}> using query <{query}>", colonyId, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddColonyId(colonyId, nameof(colonyId));
            parameters.AddLocker(holder, nameof(holder));

            var wasUpdated = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Releasing of process lock on colony <{HiveLog.Colony.IdParam}> for <{HiveLog.Colony.HolderParam}> result was: {wasUpdated}");
            return wasUpdated;
        }
        /// <inheritdoc/>
        public virtual async Task<ColonyStorageData> TryGetColonyAsync(IStorageConnection connection, [Traceable("HiveMind.Colony.Id", null)] string id, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            id = Guard.IsNotNullOrWhitespace(id);

            _logger.Log($"Fetching colony state if it exists");

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryGetColonyAsync)), x =>
            {
                return SelectColonies(x)
                       .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));
            });

            _logger.Trace($"Fetching colony state if it exists using query <{query}>");
            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddColonyId(id, nameof(id));

            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            return ReadColonies(reader, storageConnection.Environment).SingleOrDefault();
        }
        /// <inheritdoc/>
        public virtual async Task<ColonyStorageData[]> SearchColoniesAsync(IStorageConnection connection, ColonyQueryConditions queryConditions, int pageSize, int page, QueryColonyOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Selecting the next max <{pageSize}> colonies from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated search query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedColonySearchQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildColonySearchQuery(_queryProvider, queryConditions, pageSize, page, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }

            parameters.Add(nameof(pageSize), pageSize);
            parameters.Add(nameof(page), pageSize * (page - 1));

            _logger.Trace($"Selecting the next max <{pageSize}> colonies from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var colonyStorageData = ReadColonies(reader, storageConnection.Environment);

            _logger.Log($"Selected <{colonyStorageData.Length}> colonies from page <{page}> in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return colonyStorageData.ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<long> CountColoniesAsync(IStorageConnection connection, ColonyQueryConditions queryConditions, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Counting the amount of colonies in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated count query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedColonyCountQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildColonyCountQuery(x, queryConditions, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }

            _logger.Trace($"Counting the amount of colonies in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var total = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Counted <{total}> colonies in environment <{HiveLog.EnvironmentParam}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return total;
        }
        private string BuildColonySearchQuery(ISqlQueryProvider queryProvider, ColonyQueryConditions queryConditions, int pageSize, int page, QueryColonyOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            bool joinProperty = false;
            bool joinDaemon = false;
            bool joinDaemonProperty = false;

            // Select id of matching
            var selectIdQuery = queryProvider.Select<ColonyTable>().From().Column(x => x.Id)
                                             .Where(x =>
                                             {
                                                 (joinProperty, joinDaemon, joinDaemonProperty) = BuildWhereStatement(x, parameters, queryConditions.Conditions); 
                                                 return x.LastBuilder;
                                             })
                                             .Limit(x => x.Parameter(nameof(page)), x => x.Parameter(nameof(pageSize)));

            // Join if needed
            if (joinProperty) selectIdQuery.LeftJoin().Table<ColonyPropertyTable>().On(x => x.Column(x => x.Id).EqualTo.Column<ColonyPropertyTable>(x => x.ColonyId));
            if (joinDaemon) selectIdQuery.LeftJoin().Table<ColonyDaemonTable>().On(x => x.Column(x => x.Id).EqualTo.Column<ColonyDaemonTable>(x => x.ColonyId));
            if (joinDaemonProperty) selectIdQuery.LeftJoin().Table<ColonyDaemonPropertyTable>().On(x => x.Column<ColonyDaemonPropertyTable>(x => x.ColonyId).EqualTo.Column<ColonyDaemonTable>(x => x.ColonyId).And.Column<ColonyDaemonPropertyTable>(x => x.DaemonName).EqualTo.Column<ColonyDaemonTable>(x => x.Name));

            if (orderBy.HasValue)
            {
                QueryColonyOrderByTarget orderByTarget = orderBy.Value;
                switch (orderByTarget)
                {
                    case QueryColonyOrderByTarget.Id:
                        selectIdQuery.OrderBy(x => x.Id, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryColonyOrderByTarget.Name:
                        selectIdQuery.OrderBy(x => x.Name, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryColonyOrderByTarget.Status:
                        selectIdQuery.OrderBy(x => x.Status, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryColonyOrderByTarget.CreatedAt:
                        selectIdQuery.OrderBy(x => x.CreatedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    case QueryColonyOrderByTarget.ModifiedAt:
                        selectIdQuery.OrderBy(x => x.ModifiedAt, orderByDescending ? SortOrders.Descending : SortOrders.Ascending);
                        break;
                    default: throw new NotSupportedException($"Order by target <{orderByTarget}> is not supported");
                }
            }

            // Select colonies
            const string cteName = "cte";
            const string cteAlias = "c";
            var selectQuery = queryProvider.With().Cte(cteName)
                                                        .As(selectIdQuery)
                                                   .Execute(SelectColonies(queryProvider)
                                                           .InnerJoin().Table(cteName, (object)cteAlias).On(x => x.Column(c => c.Id).EqualTo.Column(cteAlias, c => c.Id)));

            return selectQuery.Build(_compileOptions);
        }
        private string BuildColonyCountQuery(ISqlQueryProvider queryProvider, ColonyQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            bool joinProperty = false;
            bool joinDaemon = false;
            bool joinDaemonProperty = false;

            var countQuery = queryProvider.Select<ColonyTable>().From();
            countQuery.Where(x =>
            {
                (joinProperty, joinDaemon, joinDaemonProperty) = BuildWhereStatement(x, parameters, queryConditions.Conditions);
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.LeftJoin().Table<ColonyPropertyTable>().On(x => x.Column(x => x.Id).EqualTo.Column<ColonyPropertyTable>(x => x.ColonyId));
            if (joinDaemon) countQuery.LeftJoin().Table<ColonyDaemonTable>().On(x => x.Column(x => x.Id).EqualTo.Column<ColonyDaemonTable>(x => x.ColonyId));
            if (joinDaemonProperty) countQuery.LeftJoin().Table<ColonyDaemonPropertyTable>().On(x => x.Column<ColonyDaemonPropertyTable>(x => x.ColonyId).EqualTo.Column<ColonyDaemonTable>(x => x.ColonyId).And.Column<ColonyDaemonPropertyTable>(x => x.DaemonName).EqualTo.Column<ColonyDaemonTable>(x => x.Name));

            // Count total matching
            countQuery.Count(x => x.Id);

            return countQuery.Build(_compileOptions);
        }
        private async Task<(bool WasLocked, LockStorageData Lock)> TrySyncAndGetProcessLockOnColonyAsync(IStorageConnection connection, ColonyStorageData colony, string requester, TimeSpan? timeout, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection, true);
            colony = Guard.IsNotNull(colony);
            requester = Guard.IsNotNullOrWhitespace(requester);

            _logger.Log($"Trying to {(timeout.HasValue ? "acquire process lock" : "sync state")} for <{requester}>");

            const string DaemonParameterSuffix = "Daemon";
            const string ColonyPropertyParameterSuffix = "Property";
            const string ColonyDaemonPropertyParameterSuffix = "DaemonProperty";
            const string ColonyIdParameter = "ColonyId";
            var amountOfDaemons = colony.Daemons?.Count ?? 0;
            var amountOfProperties = colony.Properties?.Count ?? 0;
            var daemonCacheKey = colony.Daemons.HasValue() ? colony.Daemons.Select((x, i) => $"{i}=>{(x.Properties?.Count ?? 0)}").JoinString('|') : "0";

            // Generate query 
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TrySyncAndGetProcessLockOnColonyAsync)}.{timeout.HasValue}.{amountOfProperties}.{daemonCacheKey}"), p =>
            {
                var fullQuery = p.New();
                const string wasLockedVariable = "wasLocked";

                var selectLocked = p.Select().ColumnExpression(x => x.AssignVariable(wasLockedVariable, 1));
                var selectNotLocked = p.Select().ColumnExpression(x => x.AssignVariable(wasLockedVariable, 0));

                // Try update
                var updateColony = p.Update<ColonyTable>().Table()
                                    .Set.Column(c => c.LockedBy).To.Parameter(nameof(requester))
                                    .Set.Column(c => c.LockedAt).To.CurrentDate(DateType.Utc)
                                    .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                                    .Set.Column(c => c.Name).To.Parameter(c => c.Name)
                                    .Set.Column(c => c.Status).To.Parameter(c => c.Status)
                                    .Set.Column(c => c.Options).To.Parameter(c => c.Options)
                                    .Set.Column(c => c.ModifiedAt).To.CurrentDate(DateType.Utc)
                                    .When(timeout.HasValue, x =>
                                    {
                                        return x.Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id)
                                                 .And.WhereGroup(g => g.Column(c => c.LockedBy).IsNull.Or.
                                                                        Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester)).Or.
                                                                        Column(g => g.LockHeartbeat).LesserThan.ModifyDate(d => d.CurrentDate(DateType.Utc), a => a.Expressions(null, e => e.Expression("-"), e => e.Parameter(nameof(timeout))), DateInterval.Millisecond))
                                                 );
                                    }, x =>
                                    {
                                        return x.Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id)
                                                         .And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester)));
                                    });

                var insertColony = p.Insert<ColonyTable>().Into().Columns(c => c.Id, c => c.Name, c => c.Status, c => c.Options, c => c.LockedBy, c => c.LockedAt, c => c.LockHeartbeat, c => c.CreatedAt, c => c.ModifiedAt)
                                    .Values(x => x.Parameter(c => c.Id), x => x.Parameter(c => c.Name), x => x.Parameter(c => c.Status), x => x.Parameter(c => c.Options), x => x.Parameter(nameof(requester)), x => x.CurrentDate(DateType.Utc), x => x.CurrentDate(DateType.Utc), x => x.CurrentDate(DateType.Utc), x => x.CurrentDate(DateType.Utc));

                // If not locked we need to check if row exists because if it is locked
                var insertIfNotExists = p.If().Condition(x => x.ExistsIn(p.Select<ColonyTable>().Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id))))
                                              .Then(selectNotLocked)
                                         .Else
                                              .Then(insertColony)
                                              .Then(selectLocked);

                // If row count 0 could mean row doesn't exist yet or is locked
                var ifNotUpdated = p.If().Condition(x => x.RowCount().EqualTo.Value(0))
                                        .Then(timeout.HasValue ? insertIfNotExists : selectLocked)
                                    .Else
                                        .Then(selectLocked);

                // Sync state
                var syncIfUpdatedTemp = p.If().Condition(x => x.Variable(wasLockedVariable).EqualTo.Value(1));
                IQueryBuilder syncProperties = null;
                IQueryBuilder syncDaemons = null;

                // Sync properties if present
                if (amountOfProperties > 0)
                {
                    syncProperties = CreateSyncColonyPropertiesQuery(p, amountOfProperties, ColonyPropertyParameterSuffix, true);
                }

                // Sync daemons if present
                if (amountOfDaemons > 0)
                {
                    var daemonSyncQuery = p.New();
                    var syncDaemonStates = CreateSyncDaemonQuery(p, amountOfDaemons, ColonyIdParameter, DaemonParameterSuffix);
                    daemonSyncQuery.Append(syncDaemonStates);

                    // Sync daemon properties
                    var syncDaemonProperties = CreateSyncColonyDaemonPropertiesQuery(p, colony.Daemons.Select(x => x.Properties?.Count ?? 0).ToArray(), ColonyIdParameter, DaemonParameterSuffix, ColonyDaemonPropertyParameterSuffix, true);
                    daemonSyncQuery.Append(syncDaemonProperties);

                    syncDaemons = daemonSyncQuery;
                }

                IQueryBuilder syncIfUpdated = null;
                if (syncProperties != null) syncIfUpdated = syncIfUpdatedTemp.Then(syncProperties);
                if (syncDaemons != null) syncIfUpdated = syncIfUpdatedTemp.Then(syncDaemons);

                // Select result
                var selectLockState = p.Select<ColonyTable>()
                                        .Column(c => c.LockedBy)
                                        .Column(c => c.LockedAt)
                                        .Column(c => c.LockHeartbeat)
                                       .From()
                                       .Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id));


                return fullQuery.Append(updateColony)
                                .Append(ifNotUpdated)
                                .When(syncIfUpdated != null, x => x.Append(syncIfUpdated))
                                .Append(selectLockState);
            });

            _logger.Trace($"Trying to {(timeout.HasValue ? "acquire process lock" : "sync state")} for <{requester}> using query <{query}>");

            // Parameters
            var colonyTable = new ColonyTable(colony, _hiveOptions.Get(_environment), _cache);
            var colonyPropertyTables = colony.Properties.HasValue() ? colony.Properties.Select(x => new ColonyPropertyTable(colony.Id, x)).ToArray() : null;
            var colonyDaemonTables = colony.Daemons.HasValue() ? colony.Daemons.Select(x => new ColonyDaemonTable(colony.Id, x)).ToArray() : null;
            var colonyDaemonPropertyTables = colony.Daemons.HasValue() ? colony.Daemons.Where(x => x.Properties.HasValue()).SelectMany(x => x.Properties.Select(p => (Daemon: x, Property: p))).GroupAsDictionary(x => colony.Daemons.IndexOf(x.Daemon), x => new ColonyDaemonPropertyTable(colonyTable.Id, x.Daemon.Name, x.Property)) : null;
            var parameters = new DynamicParameters();
            colonyTable.AppendSyncParameters(parameters);
            parameters.AddColonyId(colony.Id, ColonyIdParameter);
            parameters.AddLocker(requester, nameof(requester));
            if (timeout.HasValue) parameters.Add(nameof(timeout), timeout.Value.TotalMilliseconds, DbType.Double, ParameterDirection.Input);
            colonyPropertyTables.Execute((i, x) => x.AppendCreateParameters(parameters, $"{ColonyPropertyParameterSuffix}{i}"));
            colonyDaemonTables.Execute((i, x) => x.AppendSyncParameters(parameters, $"{DaemonParameterSuffix}{i}"));
            colonyDaemonPropertyTables.Execute(x =>
            {
                var daemonIndex = x.Key;
                x.Value.Execute((i, x) =>
                {
                    x.AppendCreateParameters(parameters, $"{ColonyDaemonPropertyParameterSuffix}{daemonIndex}{i}");
                });
            });

            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            var wasUpdated = await reader.ReadSingleAsync<bool>().ConfigureAwait(false);
            var lockState = await reader.ReadSingleAsync<ColonyTable>().ConfigureAwait(false);

            _logger.Log($"{(timeout.HasValue ? "Process lock" : "State sync")} result for <{requester}> is <{wasUpdated}>");
            return (wasUpdated, lockState?.ToLockStorageFormat());
        }        
        protected ColonyStorageData[] ReadColonies(SqlMapper.GridReader reader, string environment)
        {
            reader.ValidateArgument(nameof(reader));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Dictionary<string, (ColonyTable Colony, Dictionary<string, (ColonyDaemonTable Daemon, Dictionary<string, ColonyDaemonPropertyTable> Properties)> Daemons, Dictionary<string, ColonyPropertyTable> Properties)> colonies = new();

            _ = reader.Read<ColonyTable, ColonyPropertyTable, ColonyDaemonTable, ColonyDaemonPropertyTable, Null>((c, p, d, dp) =>
            {
                ColonyTable colony = null;
                Dictionary<string, (ColonyDaemonTable Daemon, Dictionary<string, ColonyDaemonPropertyTable> Properties)> daemons = null;
                Dictionary<string, ColonyPropertyTable> properties;

                if (colonies.TryGetValue(c.Id, out var existingColony))
                {
                    colony = existingColony.Colony;
                    daemons = existingColony.Daemons;
                    properties = existingColony.Properties;
                }
                else
                {
                    colony = c;
                    daemons = new Dictionary<string, (ColonyDaemonTable Daemon, Dictionary<string, ColonyDaemonPropertyTable> Properties)>();
                    properties = new Dictionary<string, ColonyPropertyTable>();
                    colonies.Add(colony.Id, (colony, daemons, properties));
                }

                if (p != null && !properties.ContainsKey(p.Name))
                {
                    properties.Add(p.Name, p);
                }

                ColonyDaemonTable daemon = null;
                Dictionary<string, ColonyDaemonPropertyTable> daemonProperties = null;
                if (d != null)
                {
                    if (daemons.TryGetValue(d.Name, out var existingDaemon))
                    {
                        daemon = existingDaemon.Daemon;
                        daemonProperties = existingDaemon.Properties;
                    }
                    else
                    {
                        daemon = d;
                        daemonProperties = new Dictionary<string, ColonyDaemonPropertyTable>();
                        daemons.Add(daemon.Name, (daemon, daemonProperties));
                    }
                }

                if (daemon != null && dp != null && !daemonProperties.ContainsKey(dp.Name))
                {
                    daemonProperties.Add(dp.Name, dp);
                }
                return Null.Value;
            }, $"{nameof(ColonyPropertyTable.ColonyId)},{nameof(ColonyDaemonTable.ColonyId)},{nameof(ColonyDaemonPropertyTable.ColonyId)}");

            // Convert to storage format
            List<ColonyStorageData> colonyStorageDatas = new List<ColonyStorageData>();
            var options = _hiveOptions.Get(environment);
            foreach (var mappedColony in colonies)
            {
                var colony = mappedColony.Value.Colony.ToStorageFormat(options, _cache);
                if (mappedColony.Value.Daemons.HasValue()) colony.Daemons = mappedColony.Value.Daemons.Select(x =>
                {
                    var daemon = x.Value.Daemon.ToStorageFormat(options, _cache);
                    if (x.Value.Properties.HasValue()) daemon.Properties = x.Value.Properties.Select(p => p.Value.ToStorageFormat()).ToList();
                    return daemon;
                }).ToList();
                if (mappedColony.Value.Properties.HasValue()) colony.Properties = mappedColony.Value.Properties.Select(x => x.Value.ToStorageFormat()).ToList();

                colonyStorageDatas.Add(colony);
            }

            return colonyStorageDatas.ToArray();
        }
        #endregion

        /// <summary>
        /// Returns the full cache key for <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key to get the full key for</param>
        /// <returns>The full cache key for <paramref name="key"/></returns>
        protected string GetCacheKey(string key)
        {
            key.ValidateArgumentNotNullOrWhitespace(nameof(key));

            return $"{_hiveOptions.Get(_environment).CachePrefix}.{nameof(HiveMindMySqlStorage)}.{key}";
        }

        #region Query Building
        private (bool requiresProperty, bool requiresState) BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable>(IStatementConditionExpressionBuilder<TJobTable> builder, DynamicParameters parameters, IEnumerable<JobConditionGroupExpression> queryConditions, string foreignKeyColumnName)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            //// Try and determine if we can just build a query using joins on some tables
            // We can only join if they are all OR statements (exception for the last)
            var propertyConditions = GetConditions(queryConditions).Where(x => x.Condition.Target == QueryJobConditionTarget.Property).ToArray();
            bool canJoinProperty = propertyConditions.Take(propertyConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);
            // Can't join if we have a not exists condition
            if (canJoinProperty && propertyConditions.Any(x => x.Condition.PropertyComparison.QueryType == PropertyConditionQueryType.NotExists)) canJoinProperty = false;

            // We can only join on state when they are all OR statements (exception for the last) unless they both target current and past states
            var stateConditions = GetConditions(queryConditions).Where(x => (x.Condition.CurrentStateComparison != null && x.Condition.CurrentStateComparison.Target != QueryJobStateConditionTarget.Property) || (x.Condition.PastStateComparison != null && x.Condition.PastStateComparison.Target != QueryJobStateConditionTarget.Property)).ToArray();
            bool onAnyState = stateConditions.Count(x => x.Condition.CurrentStateComparison != null) > 0 && stateConditions.Count(x => x.Condition.PastStateComparison != null) > 0;
            bool canJoinState = !onAnyState && stateConditions.Take(stateConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);

            var (requiresProperty, requiresState) = BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable>(builder, parameters, queryConditions, foreignKeyColumnName, canJoinProperty, canJoinState);
            return (requiresProperty && canJoinProperty, requiresState && canJoinState);
        }
        private (bool requiresProperty, bool requiresState) BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable>(IStatementConditionExpressionBuilder<TJobTable> builder, DynamicParameters parameters, IEnumerable<JobConditionGroupExpression> queryConditions, string foreignKeyColumnName, bool canJoinProperty, bool canJoinState)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            bool requiresProperty = false;
            bool requiresState = false;
            var totalCondition = queryConditions.GetCount();

            if (queryConditions.HasValue())
            {
                foreach (var (expression, logicalOperator, index) in queryConditions.Select((x, i) => (x.Expression, x.Operator, i)))
                {
                    var isLast = index == (totalCondition - 1);

                    if (expression.IsGroup)
                    {
                        builder.WhereGroup(x =>
                        {
                            var (conditionRequiresProperty, conditionRequiresState) = BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable>(x, parameters, expression.Group.Conditions, foreignKeyColumnName, canJoinProperty, canJoinState);

                            if (conditionRequiresProperty) requiresProperty = true;
                            if (conditionRequiresState) requiresState = true;

                            return x.LastBuilder;
                        });
                    }
                    else
                    {
                        var (conditionRequiresProperty, conditionRequiresState) = AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable>(builder, expression.Condition, parameters, foreignKeyColumnName, canJoinProperty, canJoinState);

                        if (conditionRequiresProperty) requiresProperty = true;
                        if (conditionRequiresState) requiresState = true;
                    }

                    if (!isLast) builder.LastBuilder.AndOr(logicalOperator.HasValue && logicalOperator.Value == QueryLogicalOperator.Or ? LogicOperators.Or : LogicOperators.And);
                }
            }

            return (requiresProperty, requiresState);
        }
        private (bool requiresProperty, bool requiresState) AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable>(IStatementConditionExpressionBuilder<TJobTable> builder, JobCondition condition, DynamicParameters parameters, string foreignKeyColumnName, bool canJoinProperty, bool canJoinState)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresProperty = false;
            bool requiresState = false;

            switch (condition.Target)
            {
                case QueryJobConditionTarget.Queue:
                    AddComparison(builder, x => x.Column(x => x.Queue), condition.QueueComparison, parameters);
                    break;
                case QueryJobConditionTarget.LockedBy:
                    AddComparison(builder, x => x.Column(x => x.LockedBy), condition.LockedByComparison, parameters);
                    break;
                case QueryJobConditionTarget.Priority:
                    AddComparison(builder, x => x.Column(x => x.Priority), condition.PriorityComparison, parameters);
                    break;
                case QueryJobConditionTarget.CreatedAt:
                    AddComparison(builder, x => x.Column(x => x.CreatedAt), condition.CreatedAtComparison, parameters);
                    break;
                case QueryJobConditionTarget.ModifiedAt:
                    AddComparison(builder, x => x.Column(x => x.ModifiedAt), condition.ModifiedAtComparison, parameters);
                    break;
                case QueryJobConditionTarget.Property:
                    // Exists in because we can join property table
                    if (!canJoinProperty)
                    {
                        var propertyBuilder = _queryProvider.Select<TJobPropertyTable>().Value(1).From().Where(x =>
                        {
                            var b = x.Column(typeof(TJobPropertyTable), foreignKeyColumnName).EqualTo.Column<TJobTable>(x => x.Id);
                            if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.Value)
                            {
                                AddComparison<TJobPropertyTable, TJobPropertyTable>(b.And, condition.PropertyComparison, parameters);
                            }
                            return b;
                        });
                        if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.NotExists)
                        {
                            builder.Not().ExistsIn(propertyBuilder);
                        }
                        else
                        {
                            builder.ExistsIn(propertyBuilder);
                        }
                    }
                    else if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.Exists)
                    {
                        requiresProperty = true;
                        var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                        parameters?.Add(parameter, condition.PropertyComparison.Name);
                        builder.WhereGroup(x => x.Column(typeof(TJobPropertyTable), foreignKeyColumnName).EqualTo.Column(x => x.Id).And.Column<TJobPropertyTable>(x => x.Name).EqualTo.Parameter(parameter));
                    }
                    else
                    {
                        requiresProperty = true;
                        AddComparison<TJobTable, TJobPropertyTable>(builder, condition.PropertyComparison, parameters);
                    }

                    break;
                case QueryJobConditionTarget.CurrentState:
                    requiresState = true;
                    AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobTable>(builder, condition.CurrentStateComparison, parameters, foreignKeyColumnName, true, canJoinState);
                    break;
                case QueryJobConditionTarget.PastState:
                    requiresState = true;
                    AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobTable>(builder, condition.PastStateComparison, parameters, foreignKeyColumnName, false, canJoinState);
                    break;
                default: throw new NotSupportedException($"Condition target <{condition.Target}> is not known");
            }

            return (requiresProperty, requiresState);
        }
        private void AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, T>(IStatementConditionExpressionBuilder<T> builder, JobStateCondition condition, DynamicParameters parameters, string foreignKeyColumnName, bool isCurrentState, bool canJoinState)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            if (canJoinState)
            {
                builder.WhereGroup(x =>
                {
                    _ = x.Column<TJobStateTable>(c => c.IsCurrent).EqualTo.Value(isCurrentState).And;

                    AddComparison<TJobStateTable, T>(x, condition, parameters);
                    return x.LastBuilder;
                });
            }
            else
            {
                var subBuilder = _queryProvider.Select<TJobStateTable>().Value(1).From()
                                .Where(x =>
                                {
                                    var b = x.Column(typeof(TJobStateTable), foreignKeyColumnName).EqualTo.Column<TJobTable>(x => x.Id).And.Column(x => x.IsCurrent).EqualTo.Value(isCurrentState).And;
                                    AddComparison<TJobStateTable, TJobStateTable>(b, condition, parameters);
                                    return b.LastBuilder;
                                });
                builder.ExistsIn(subBuilder);
            }
        }
        private void AddComparison<TJobStateTable, T>(IStatementConditionExpressionBuilder<T> builder, JobStateCondition condition, DynamicParameters parameters)
            where TJobStateTable : BaseStateTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            switch (condition.Target)
            {
                case QueryJobStateConditionTarget.Name:
                    AddComparison(builder, x => x.Column<TJobStateTable>(x => x.Name), condition.NameComparison, parameters);
                    break;
                case QueryJobStateConditionTarget.Reason:
                    AddComparison(builder, x => x.Column<TJobStateTable>(x => x.Reason), condition.ReasonComparison, parameters);
                    break;
                case QueryJobStateConditionTarget.ElectedDate:
                    AddComparison(builder, x => x.Column<TJobStateTable>(x => x.ElectedDate), condition.ElectedDateComparison, parameters);
                    break;
                default: throw new NotSupportedException($"Target <{condition.Target}> is not supported");
            }
        }
        private IEnumerable<(JobCondition Condition, QueryLogicalOperator? Operator)> GetConditions(IEnumerable<JobConditionGroupExpression> expressions)
        {
            foreach (var expression in expressions)
            {
                if (expression.Expression.IsGroup)
                {
                    foreach (var subCondition in GetConditions(expression.Expression.Group.Conditions))
                    {
                        yield return (subCondition.Condition, subCondition.Operator);
                    }
                }
                else
                {
                    yield return (expression.Expression.Condition, expression.Operator);
                }
            }
        }
        private void AddParameters(JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            foreach (var (condition, _) in GetConditions(queryConditions.Conditions))
            {
                switch (condition.Target)
                {
                    case QueryJobConditionTarget.Queue:
                        AddParameters(condition.QueueComparison, parameters);
                        break;
                    case QueryJobConditionTarget.LockedBy:
                        AddParameters(condition.LockedByComparison, parameters);
                        break;
                    case QueryJobConditionTarget.Priority:
                        AddParameters(condition.PriorityComparison, parameters);
                        break;
                    case QueryJobConditionTarget.CreatedAt:
                        AddParameters(condition.CreatedAtComparison, parameters);
                        break;
                    case QueryJobConditionTarget.ModifiedAt:
                        AddParameters(condition.ModifiedAtComparison, parameters);
                        break;
                    case QueryJobConditionTarget.Property:
                        AddParameters(condition.PropertyComparison, parameters);
                        break;
                    case QueryJobConditionTarget.CurrentState:
                        AddParameters(condition.CurrentStateComparison, queryConditions, parameters);
                        break;
                    case QueryJobConditionTarget.PastState:
                        AddParameters(condition.PastStateComparison, queryConditions, parameters);
                        break;
                    default: throw new NotSupportedException($"Condition target <{condition.Target}> is not known");
                }

            }
        }
        private void AddParameters(JobStateCondition condition, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            condition.ValidateArgument(nameof(condition));
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            switch (condition.Target)
            {
                case QueryJobStateConditionTarget.Name:
                    AddParameters(condition.NameComparison, parameters);
                    break;
                case QueryJobStateConditionTarget.Reason:
                    AddParameters(condition.ReasonComparison, parameters);
                    break;
                case QueryJobStateConditionTarget.ElectedDate:
                    AddParameters(condition.ElectedDateComparison, parameters);
                    break;
                case QueryJobStateConditionTarget.Property:
                    AddParameters(condition.PropertyComparison, parameters);
                    break;
            }
        }

        private (bool requiresProperty, bool requiresDaemon, bool requiresDaemonProperty) BuildWhereStatement(IStatementConditionExpressionBuilder<ColonyTable> builder, DynamicParameters parameters, IEnumerable<ColonyConditionGroupExpression> queryConditions)
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            //// Try and determine if we can just build a query using joins on some tables
            // We can only join colony property if they are all OR statements (exception for the last)
            var propertyConditions = GetConditions(queryConditions).Where(x => x.Condition.Target == QueryColonyConditionTarget.Property).ToArray();
            bool canJoinProperty = propertyConditions.Take(propertyConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);
            // Can't join if we have a not exists condition
            if (canJoinProperty && propertyConditions.Any(x => x.Condition.PropertyComparison.QueryType == PropertyConditionQueryType.NotExists)) canJoinProperty = false;

            // We can only join colony daemon if they are all OR statements (exception for the last)
            var daemonConditions = GetConditions(queryConditions).Where(x => x.Condition.Target == QueryColonyConditionTarget.Daemon).ToArray();
            bool canJoinDaemon = daemonConditions.Take(daemonConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);

            // We can only join colony daemon property if they are all OR statements (exception for the last)
            var daemonPropertyConditions = GetConditions(queryConditions).Where(x => x.Condition.Target == QueryColonyConditionTarget.Daemon).Where(x => x.Condition.DaemonCondition.Target == QueryColonyDaemonConditionTarget.Property).ToArray();
            bool canJoinDaemonProperty = daemonPropertyConditions.Take(daemonPropertyConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);
            // Can't join if we have a not exists condition
            if (canJoinDaemonProperty && propertyConditions.Any(x => x.Condition.DaemonCondition.PropertyComparison.QueryType == PropertyConditionQueryType.NotExists)) canJoinProperty = false;

            // Can't join daemon property if we can't join daemon.
            if (!canJoinDaemon) canJoinDaemonProperty = false;

            var (requiresProperty, requiresDaemon, requiresDaemonProperty) =  BuildWhereStatement(builder, parameters, queryConditions, canJoinProperty, canJoinDaemon, canJoinDaemonProperty);

            return (canJoinProperty && requiresProperty, canJoinDaemon && requiresDaemon, canJoinDaemonProperty && requiresDaemonProperty);
        }
        private (bool requiresProperty, bool requiresDaemon, bool requiresDaemonProperty) BuildWhereStatement(IStatementConditionExpressionBuilder<ColonyTable> builder, DynamicParameters parameters, IEnumerable<ColonyConditionGroupExpression> queryConditions, bool canJoinProperty, bool canJoinDaemon, bool canJoinDaemonProperty)
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            bool requiresProperty = false;
            bool requiresDaemon = false;
            bool requiresDaemonProperty = false;
            var totalCondition = queryConditions.GetCount();

            if (queryConditions.HasValue())
            {
                foreach (var (expression, logicalOperator, index) in queryConditions.Select((x, i) => (x.Expression, x.Operator, i)))
                {
                    var isLast = index == (totalCondition - 1);

                    if (expression.IsGroup)
                    {
                        builder.WhereGroup(x =>
                        {
                            var (conditionRequiresProperty, conditionRequiresDaemon, conditionRequiresDaemonProperty) = BuildWhereStatement(x, parameters, expression.Group.Conditions, canJoinProperty, canJoinDaemon, canJoinDaemonProperty);

                            if (conditionRequiresProperty) requiresProperty = true;
                            if (conditionRequiresDaemon) requiresDaemon = true;
                            if (conditionRequiresDaemonProperty) requiresDaemonProperty = true;

                            return x.LastBuilder;
                        });
                    }
                    else
                    {
                        var (conditionRequiresProperty, conditionRequiresDaemon, conditionRequiresDaemonProperty) = AddCondition(builder, expression.Condition, parameters, canJoinProperty, canJoinDaemon, canJoinDaemonProperty);

                        if (conditionRequiresProperty) requiresProperty = true;
                        if (conditionRequiresDaemon) requiresDaemon = true;
                        if (conditionRequiresDaemonProperty) requiresDaemonProperty = true;
                    }

                    if (!isLast) builder.LastBuilder.AndOr(logicalOperator.HasValue && logicalOperator.Value == QueryLogicalOperator.Or ? LogicOperators.Or : LogicOperators.And);
                }
            }

            return (requiresProperty, requiresDaemon, requiresDaemonProperty);
        }
        private (bool requiresProperty, bool requiresDaemon, bool requiresDaemonProperty) AddCondition(IStatementConditionExpressionBuilder<ColonyTable> builder, ColonyCondition condition, DynamicParameters parameters, bool canJoinProperty, bool canJoinDaemon, bool canJoinDaemonProperty)
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresProperty = false;
            bool requiresDaemon = false;
            bool requiresDaemonProperty = false;

            switch (condition.Target)
            {
                case QueryColonyConditionTarget.Name:
                    AddComparison(builder, x => x.Column(x => x.Name), condition.NameComparison, parameters);
                    break;
                case QueryColonyConditionTarget.Status:
                    AddComparison(builder, x => x.Column(x => x.Status), condition.StatusComparison, parameters);
                    break;
                case QueryColonyConditionTarget.CreatedAt:
                    AddComparison(builder, x => x.Column(x => x.CreatedAt), condition.CreatedAtComparison, parameters);
                    break;
                case QueryColonyConditionTarget.ModifiedAt:
                    AddComparison(builder, x => x.Column(x => x.ModifiedAt), condition.ModifiedAtComparison, parameters);
                    break;
                case QueryColonyConditionTarget.Property:
                    // Exists in because we can't join property table
                    if (!canJoinProperty)
                    {
                        var propertyBuilder = _queryProvider.Select<ColonyPropertyTable>().Value(1).From().Where(x =>
                        {
                            var b = x.Column(c => c.ColonyId).EqualTo.Column<ColonyTable>(x => x.Id);
                            if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.Value)
                            {
                                AddComparison<ColonyPropertyTable, ColonyPropertyTable>(b.And, condition.PropertyComparison, parameters);
                            }
                            return b;
                        });
                        if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.NotExists)
                        {
                            builder.Not().ExistsIn(propertyBuilder);
                        }
                        else
                        {
                            builder.ExistsIn(propertyBuilder);
                        }
                    }
                    else if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.Exists)
                    {
                        requiresProperty = true;
                        var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                        parameters?.Add(parameter, condition.PropertyComparison.Name);
                        builder.WhereGroup(x => x.Column<ColonyPropertyTable>(c => c.ColonyId).EqualTo.Column(x => x.Id).And.Column(x => x.Name).EqualTo.Parameter(parameter));
                    }
                    else
                    {
                        requiresProperty = true;
                        AddComparison<ColonyTable, ColonyPropertyTable>(builder, condition.PropertyComparison, parameters);
                    }

                    break;
                case QueryColonyConditionTarget.Daemon:
                    var (conditionRequiresDaemon, conditionRequiresDaemonProperty) = AddComparison(builder, condition.DaemonCondition, parameters, canJoinDaemon, canJoinDaemonProperty);
                    if (conditionRequiresDaemon) requiresDaemon = true;
                    if (conditionRequiresDaemonProperty) requiresDaemonProperty = true;
                    break;
                default: throw new NotSupportedException($"Condition target <{condition.Target}> is not known");
            }

            return (requiresProperty, requiresDaemon, requiresDaemonProperty);
        }
        private (bool requiresDaemon, bool requiresProperty) AddComparison<T>(IStatementConditionExpressionBuilder<T> builder, ColonyDaemonCondition condition, DynamicParameters parameters, bool canJoinDaemon, bool canJoinDaemonProperty)
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresDaemon = false;
            bool requiresProperty = false;

            switch ((Target: condition.Target, CanJoin: canJoinDaemon))
            {
                case (QueryColonyDaemonConditionTarget.Name, true):
                    requiresDaemon = true;
                    AddComparison(builder, x => x.Column<ColonyDaemonTable>(x => x.Name), condition.NameComparison, parameters);
                    break;
                case (QueryColonyDaemonConditionTarget.Status, true):
                    requiresDaemon = true;
                    AddComparison(builder, x => x.Column<ColonyDaemonTable>(x => x.Status), condition.StatusComparison, parameters);
                    break;
                case (QueryColonyDaemonConditionTarget.CreatedAt, true):
                    requiresDaemon = true;
                    AddComparison(builder, x => x.Column<ColonyDaemonTable>(x => x.CreatedAt), condition.CreatedAtComparison, parameters);
                    break;
                case (QueryColonyDaemonConditionTarget.ModifiedAt, true):
                    requiresDaemon = true;
                    AddComparison(builder, x => x.Column<ColonyDaemonTable>(x => x.ModifiedAt), condition.ModifiedAtComparison, parameters);
                    break;
                case (QueryColonyDaemonConditionTarget Target, bool CanJoin) propertyCase when propertyCase.Target == QueryColonyDaemonConditionTarget.Property && !canJoinDaemonProperty:
                    // Exists in because we can't join property table
                    var propertyBuilder = _queryProvider.Select<ColonyDaemonPropertyTable>().Value(1).From().Where(x =>
                    {
                        var b = x.Column(c => c.ColonyId).EqualTo.Column<ColonyTable>(x => x.Id);
                        if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.Value)
                        {
                            AddComparison<ColonyDaemonPropertyTable, ColonyDaemonPropertyTable>(b.And, condition.PropertyComparison, parameters);
                        }
                        return b;
                    });
                    if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.NotExists)
                    {
                        builder.Not().ExistsIn(propertyBuilder);
                    }
                    else
                    {
                        builder.ExistsIn(propertyBuilder);
                    }

                    break;
                case (QueryColonyDaemonConditionTarget Target, bool CanJoin) propertyCase when propertyCase.Target == QueryColonyDaemonConditionTarget.Property && propertyCase.CanJoin:
                    if (condition.PropertyComparison.QueryType == PropertyConditionQueryType.Exists)
                    {
                        requiresProperty = true;
                        var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                        parameters?.Add(parameter, condition.PropertyComparison.Name);
                        builder.WhereGroup(x => x.Column<ColonyDaemonPropertyTable>(c => c.ColonyId).EqualTo.Column<ColonyTable>(x => x.Id).And.Column<ColonyDaemonPropertyTable>(x => x.Name).EqualTo.Parameter(parameter));
                    }
                    else
                    {
                        requiresProperty = true;
                        AddComparison<T, ColonyDaemonPropertyTable>(builder, condition.PropertyComparison, parameters);
                    }
                    break;
                case (QueryColonyDaemonConditionTarget Target, bool CanJoin) propertyCase when !propertyCase.CanJoin:
                    bool needsProperty = false;
                    var subBuilder = _queryProvider.Select<ColonyTable>().Value(1).From().Where(x =>
                    {
                        var b = x.Column(c => c.Id).EqualTo.Column<ColonyDaemonTable>(x => x.ColonyId);
                        (_, needsProperty) = AddComparison(b.And, condition, parameters, true, true);
                        return b;
                    });
                    subBuilder.LeftJoin().Table<ColonyDaemonTable>().On(x => x.Column<ColonyDaemonTable>(x => x.ColonyId).EqualTo.Column<ColonyTable>(x => x.Id));
                    if (needsProperty) subBuilder.LeftJoin().Table<ColonyDaemonPropertyTable>().On(x => x.Column<ColonyDaemonPropertyTable>(x => x.ColonyId).EqualTo.Column<ColonyDaemonTable>(x => x.ColonyId).And.Column<ColonyDaemonPropertyTable>(x => x.DaemonName).EqualTo.Column<ColonyDaemonTable>(x => x.Name));
                    builder.ExistsIn(subBuilder);
                    break;
                default: throw new NotSupportedException($"Target <{condition.Target}> is not supported");
            }

            return (requiresDaemon, requiresProperty);
        }
        private IEnumerable<(ColonyCondition Condition, QueryLogicalOperator? Operator)> GetConditions(IEnumerable<ColonyConditionGroupExpression> expressions)
        {
            foreach (var expression in expressions)
            {
                if (expression.Expression.IsGroup)
                {
                    foreach (var subCondition in GetConditions(expression.Expression.Group.Conditions))
                    {
                        yield return (subCondition.Condition, subCondition.Operator);
                    }
                }
                else
                {
                    yield return (expression.Expression.Condition, expression.Operator);
                }
            }
        }
        private void AddParameters(ColonyQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            foreach (var (condition, _) in GetConditions(queryConditions.Conditions))
            {
                switch (condition.Target)
                {
                    case QueryColonyConditionTarget.Name:
                        AddParameters(condition.NameComparison, parameters);
                        break;
                    case QueryColonyConditionTarget.Status:
                        AddParameters(condition.StatusComparison, parameters);
                        break;
                    case QueryColonyConditionTarget.CreatedAt:
                        AddParameters(condition.CreatedAtComparison, parameters);
                        break;
                    case QueryColonyConditionTarget.ModifiedAt:
                        AddParameters(condition.ModifiedAtComparison, parameters);
                        break;
                    case QueryColonyConditionTarget.Property:
                        AddParameters(condition.PropertyComparison, parameters);
                        break;
                    case QueryColonyConditionTarget.Daemon:
                        AddParameters(condition.DaemonCondition, queryConditions, parameters);
                        break;
                    default: throw new NotSupportedException($"Condition target <{condition.Target}> is not known");
                }

            }
        }
        private void AddParameters(ColonyDaemonCondition condition, ColonyQueryConditions queryConditions, DynamicParameters parameters)
        {
            condition.ValidateArgument(nameof(condition));
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            switch (condition.Target)
            {
                case QueryColonyDaemonConditionTarget.Name:
                    AddParameters(condition.NameComparison, parameters);
                    break;
                case QueryColonyDaemonConditionTarget.Status:
                    AddParameters(condition.StatusComparison, parameters);
                    break;
                case QueryColonyDaemonConditionTarget.CreatedAt:
                    AddParameters(condition.CreatedAtComparison, parameters);
                    break;
                case QueryColonyDaemonConditionTarget.ModifiedAt:
                    AddParameters(condition.ModifiedAtComparison, parameters);
                    break;
                case QueryColonyDaemonConditionTarget.Property:
                    AddParameters(condition.PropertyComparison, parameters);
                    break;
            }
        }


        private void AddComparison<T>(IStatementConditionExpressionBuilder<T> builder, Func<IStatementConditionExpressionBuilder<T>, IStatementConditionOperatorExpressionBuilder<T>> target, QueryComparison comparison, DynamicParameters parameters)
        {
            builder.ValidateArgument(nameof(builder));
            target.ValidateArgument(nameof(target));
            comparison.ValidateArgument(nameof(comparison));
            parameters.ValidateArgument(nameof(parameters));

            IStatementConditionRightExpressionBuilder<T> expressionBuilder = null;

            switch (comparison.Comparator)
            {
                case QueryComparator.Equals:

                    if (comparison.Value == null)
                    {
                        if (comparison.IsInverted)
                        {
                            _ = target(builder).IsNotNull;
                            return;
                        }
                        else
                        {
                            _ = target(builder).IsNull;
                            return;
                        }
                    }
                    if (comparison.IsInverted)
                    {
                        expressionBuilder = target(builder).NotEqualTo;
                    }
                    else
                    {
                        expressionBuilder = target(builder).EqualTo;
                    }
                    break;
                case QueryComparator.GreaterThan:

                    if (comparison.IsInverted)
                    {
                        builder = builder.Not();
                    }

                    expressionBuilder = target(builder).GreaterThan;
                    break;
                case QueryComparator.GreaterOrEqualTo:

                    if (comparison.IsInverted)
                    {
                        builder = builder.Not();
                    }

                    expressionBuilder = target(builder).GreaterOrEqualTo;
                    break;
                case QueryComparator.LesserThan:

                    if (comparison.IsInverted)
                    {
                        builder = builder.Not();
                    }

                    expressionBuilder = target(builder).LesserThan;
                    break;
                case QueryComparator.LesserOrEqualTo:

                    if (comparison.IsInverted)
                    {
                        builder = builder.Not();
                    }

                    expressionBuilder = target(builder).LesserOrEqualTo;
                    break;
                case QueryComparator.In:
                    var parameterNames = comparison.Values.Select((x, i) =>
                    {
                        var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                        parameters?.Add(parameter, x);
                        return parameter;
                    });
                    if (comparison.IsInverted)
                    {
                        _ = target(builder).NotIn.Parameters(parameterNames);
                    }
                    else
                    {
                        _ = target(builder).In.Parameters(parameterNames);
                    }
                    return;
                case QueryComparator.Like:
                    if (comparison.IsInverted)
                    {
                        expressionBuilder = target(builder).NotLike;
                    }
                    else
                    {
                        expressionBuilder = target(builder).Like;
                    }

                    var pattern = comparison.Pattern.Select(x => x.EqualsNoCase(HiveMindConstants.Query.Wildcard.ToString()) ? "%" : x).JoinString(string.Empty);
                    var patternParameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                    expressionBuilder.Parameter(patternParameter);
                    parameters?.Add(patternParameter, pattern);
                    return;
                default: throw new NotSupportedException($"Comparator <{comparison.Comparator}> is not supported");
            }

            var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
            expressionBuilder.Parameter(parameter);
            parameters?.Add(parameter, comparison.Value);
        }
        private void AddComparison<TBuilder, TTable>(IStatementConditionExpressionBuilder<TBuilder> builder, PropertyCondition condition, DynamicParameters parameters)
            where TTable : BasePropertyTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
            parameters?.Add(parameter, condition.Name);

            builder.WhereGroup(x =>
            {
                x = x.Column<TTable>(x => x.Name).EqualTo.Parameter(parameter).And;

                switch (condition.Type)
                {
                    case StorageType.Number:
                        AddComparison(x, x => x.Column<TTable>(x => x.NumberValue), condition.Comparison, parameters);
                        break;
                    case StorageType.FloatingNumber:
                        AddComparison(x, x => x.Column<TTable>(x => x.FloatingNumberValue), condition.Comparison, parameters);
                        break;
                    case StorageType.Date:
                        AddComparison(x, x => x.Column<TTable>(x => x.DateValue), condition.Comparison, parameters);
                        break;
                    case StorageType.Text:
                        AddComparison(x, x => x.Column<TTable>(x => x.TextValue), condition.Comparison, parameters);
                        break;
                    case StorageType.Bool:
                        AddComparison(x, x => x.Column<TTable>(x => x.BooleanValue), condition.Comparison, parameters);
                        break;
                    default: throw new NotSupportedException($"Storage type <{condition.Type}> is not supported");
                }

                return x.LastBuilder;
            });
        }
        private void AddParameters(PropertyCondition propertyCondition, DynamicParameters parameters)
        {
            propertyCondition.ValidateArgument(nameof(propertyCondition));
            parameters.ValidateArgument(nameof(parameters));

            var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
            parameters.Add(parameter, propertyCondition.Name);

            if (propertyCondition.Comparison != null) AddParameters(propertyCondition.Comparison, parameters);
        }
        private void AddParameters(QueryComparison queryComparison, DynamicParameters parameters)
        {
            queryComparison.ValidateArgument(nameof(queryComparison));
            parameters.ValidateArgument(nameof(parameters));

            switch (queryComparison.Comparator)
            {
                case QueryComparator.Equals:
                case QueryComparator.GreaterThan:
                case QueryComparator.GreaterOrEqualTo:
                case QueryComparator.LesserThan:
                case QueryComparator.LesserOrEqualTo:
                    var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                    parameters.Add(parameter, queryComparison.Value);
                    break;
                case QueryComparator.In:
                    queryComparison.Values.Execute(x =>
                    {
                        var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                        parameters.Add(parameter, x);
                    });
                    break;
                case QueryComparator.Like:

                    var pattern = queryComparison.Pattern.Select(x => x.EqualsNoCase(HiveMindConstants.Query.Wildcard.ToString()) ? "%" : x).JoinString(string.Empty);
                    var patternParameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
                    parameters.Add(patternParameter, pattern);
                    return;
            }
        }
        #endregion
    }
}
