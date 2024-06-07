using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Newtonsoft.Json.Linq;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage.Job;
using Sels.SQL.QueryBuilder;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.Core.Data.SQL.Extensions.Dapper;
using System.Linq;
using Sels.Core.Extensions.Linq;
using Sels.SQL.QueryBuilder.Builder;
using Sels.Core.Extensions.Text;
using Sels.Core.Models;
using Sels.SQL.QueryBuilder.Expressions;
using Sels.Core.Conversion.Extensions;
using System.Security.Cryptography;
using Sels.Core.Parameters;
using System.Data;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Microsoft.Extensions.Caching.Memory;
using Sels.HiveMind.Query.Job;
using Sels.SQL.QueryBuilder.Builder.Statement;
using Sels.HiveMind.Query;
using Sels.HiveMind.Storage.Sql.Templates;
using Sels.Core;
using Sels.Core.Extensions.DateTimes;
using Azure.Core;
using static Sels.HiveMind.HiveMindConstants;
using Azure;
using Sels.HiveMind.Job.State;
using Sels.Core.Extensions.Collections;
using static Sels.Core.Data.MySQL.MySqlHelper;
using Sels.Core.Extensions.Fluent;
using Sels.Core.Scope;
using static System.Collections.Specialized.BitVector32;
using Sels.HiveMind.Storage.Sql;
using Sels.HiveMind.Storage.Sql.Job.Background;
using Sels.SQL.QueryBuilder.Builder.Expressions;
using Sels.HiveMind.Storage.Sql.Job.Recurring;
using Sels.Core.Extensions.Reflection;
using static Sels.HiveMind.HiveLog;

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
                }

                // Rename tables to their actual names
                x.OnCompiling(x =>
                {
                    if(TableNames != null && x is TableExpression tableExpression && tableExpression.Alias?.Set != null)
                    {
                        switch(tableExpression.Alias.Set)
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
            _logger.Log($"Opening new connection to MySql storage in environment <{HiveLog.Environment}>", _environment);

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

            _logger.Log($"Acquiring distributed lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);

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
                _logger.Trace($"Acquiring distributed lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{lockQuery}>", jobId, connection.Environment);
                _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(lockQuery, transaction: storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Log($"Acquired distributed lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobId, connection.Environment);
            }, async () =>
            {
                _logger.Trace($"Releasing distributed lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{unlockQuery}>", jobId, connection.Environment);
                _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(unlockQuery, transaction: storageConnection.MySqlTransaction)).ConfigureAwait(false);
                _logger.Log($"Released distributed lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobId, connection.Environment);
            });

            return lockAction.StartAsync();
        }
        /// <inheritdoc/>
        public virtual async Task<string> CreateBackgroundJobAsync(BackgroundJobStorageData jobData, IStorageConnection connection, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Inserting new background job in environment <{HiveLog.Environment}>", _environment);
            // Job
            var job = new BackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateBackgroundJobAsync)), x =>
            {
                var insert = x.Insert<BackgroundJobTable>().Into(table: TableNames.BackgroundJobTable)
                              .Columns(x => x.ExecutionId, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.CreatedAt, x => x.ModifiedAt)
                              .Parameters(x => x.ExecutionId, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.CreatedAt, x => x.ModifiedAt);
                var select = x.Select().LastInsertedId();

                return x.New().Append(insert).Append(select);
            });
            var backgroundJobTable = new BackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            var parameters = backgroundJobTable.ToCreateParameters();
            _logger.Trace($"Inserting new background job in environment <{HiveLog.Environment}> using query <{query}>", _environment);

            var id = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            // States
            if (jobData.States.HasValue())
            {
                var states = jobData.States.Select(x => (new BackgroundJobStateTable(x), x.Properties.Select(x => new BackgroundJobStatePropertyTable(x)).ToArray())).ToArray();

                await InsertStatesWithPropertiesAsync(storageConnection, id, states, token).ConfigureAwait(false);
            }

            // Properties
            if (jobData.Properties.HasValue())
            {
                var properties = jobData.Properties.Select(x => new BackgroundJobPropertyTable(x)).ToArray();
                await InsertPropertiesAsync(storageConnection, id, properties, token).ConfigureAwait(false);
            }
            _logger.Log($"Inserted background job <{id}> in environment <{HiveLog.Environment}>", _environment);
            return id.ToString();
        }
        /// <inheritdoc/>
        public virtual async Task<bool> TryUpdateBackgroundJobAsync(BackgroundJobStorageData jobData, IStorageConnection connection, bool releaseLock, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Updating background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobData.Id, _environment);
            var holder = jobData.Lock.LockedBy;
            var backgroundJobTable = new BackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryUpdateBackgroundJobAsync)), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.Queue).To.Parameter(c => c.Queue)
                        .Set.Column(c => c.Priority).To.Parameter(c => c.Priority)
                        .Set.Column(c => c.ExecutionId).To.Parameter(c => c.ExecutionId)
                        .Set.Column(c => c.ModifiedAt).To.Parameter(c => c.ModifiedAt)
                        .Set.Column(c => c.LockedBy).To.Parameter(c => c.LockedBy)
                        .Set.Column(c => c.LockedAt).To.Parameter(c => c.LockedAt)
                        .Set.Column(c => c.LockHeartbeat).To.Parameter(c => c.LockHeartbeat)
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id)
                                     .And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Updating background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", jobData.Id, _environment);

            // Execute query
            var parameters = backgroundJobTable.ToUpdateParameters(holder, releaseLock);
            parameters.AddLocker(holder, nameof(holder));

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated != 1)
            {
                _logger.Warning($"Could not update background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobData.Id, _environment);
                return false;
            }
            else
            {
                var backgroundJobId = jobData.Id.ConvertTo<long>();
                // Persist new states
                if (jobData.ChangeTracker.NewStates.HasValue())
                {
                    var states = jobData.ChangeTracker.NewStates.Select(x => (new BackgroundJobStateTable(x), x.Properties.Select(x => new BackgroundJobStatePropertyTable(x)).ToArray())).ToArray();

                    await InsertStatesWithPropertiesAsync(storageConnection, backgroundJobId, states, token).ConfigureAwait(false);
                }

                // Persist changes to properties
                if (jobData.ChangeTracker.NewProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.NewProperties.Select(x => new BackgroundJobPropertyTable(x)).ToArray();
                    await InsertPropertiesAsync(storageConnection, backgroundJobId, properties, token).ConfigureAwait(false);
                }
                if (jobData.ChangeTracker.UpdatedProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.UpdatedProperties.Select(x => new BackgroundJobPropertyTable(x)).ToArray();
                    await UpdatePropertiesAsync(storageConnection, backgroundJobId, properties, token).ConfigureAwait(false);
                }
                if (jobData.ChangeTracker.RemovedProperties.HasValue())
                {
                    var properties = jobData.ChangeTracker.RemovedProperties.ToArray();
                    await DeletePropertiesAsync(storageConnection, backgroundJobId, properties, token).ConfigureAwait(false);
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
            using var methodLogger = _logger.TraceMethod(this);
            _logger.Log($"Inserting <{states.Length}> new states for background job <{HiveLog.Job.Id}>", backgroundJobId);

            var parameters = new DynamicParameters();

            // Reset is current on existing states
            var resetQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(InsertStatesWithPropertiesAsync)}.Reset"), x =>
            {
                return x.Update<BackgroundJobStateTable>().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable))
                        .Set.Column(c => c.IsCurrent).To.Value(false)
                        .Where(w => w.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)))
                        .Build(_compileOptions);
            });

            parameters.Add(nameof(backgroundJobId), backgroundJobId, DbType.Int64);
            _logger.Trace($"Resetting {nameof(BackgroundJobStateTable.IsCurrent)} to false for existing states for background job <{HiveLog.Job.Id}> using query <{resetQuery}>", backgroundJobId);
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
                _logger.Debug($"Inserting state <{HiveLog.Job.State}> for background job <{HiveLog.Job.Id}> with <{propertyCount}> properties", state.Name, backgroundJobId);
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
                                                    x => x.Parameter(x => x.Name, i),
                                                    x => x.Parameter(x => x.Type, i),
                                                    x => x.Parameter(x => x.OriginalType, i),
                                                    x => x.Parameter(x => x.TextValue, i),
                                                    x => x.Parameter(x => x.NumberValue, i),
                                                    x => x.Parameter(x => x.FloatingNumberValue, i),
                                                    x => x.Parameter(x => x.DateValue, i),
                                                    x => x.Parameter(x => x.BooleanValue, i),
                                                    x => x.Parameter(x => x.OtherValue, i));
                        });

                        query.Append(insertProperties);
                    }

                    return query;
                });
                properties.Execute((i, x) => x.AppendCreateParameters(parameters, i));
                _logger.Trace($"Inserting state <{HiveLog.Job.State}> for background job <{HiveLog.Job.Id}> with <{propertyCount}> properties using query <{query}>", state.Name, backgroundJobId);

                state.Id = await connection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Debug($"Inserted state <{HiveLog.Job.State}> for background job <{HiveLog.Job.Id}> with id <{state.Id}> with <{propertyCount}> properties", state.Name, backgroundJobId);
                properties.Execute(x => x.StateId = state.Id);
            }

            _logger.Log($"Inserted <{states.Length}> new states for background job <{HiveLog.Job.Id}>", backgroundJobId);
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
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Inserting <{properties.Length}> new properties for background job <{HiveLog.Job.Id}>", backgroundJobId);
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
                Enumerable.Range(0, properties.Length).Execute(x => insertQuery.Parameters(x, c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt));
                return insertQuery;
            });
            _logger.Trace($"Inserting <{properties.Length}> properties for background job <{HiveLog.Job.Id}> using query <{query}>", backgroundJobId);

            properties.Execute((i, x) => x.AppendCreateParameters(parameters, i));
            var inserted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            if (inserted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be inserted but only <{inserted}> were inserted");
            _logger.Log($"Inserted <{inserted}> new properties for background job <{HiveLog.Job.Id}>", backgroundJobId);
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
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Updating <{properties.Length}> properties for background job <{HiveLog.Job.Id}>", backgroundJobId);
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
            _logger.Trace($"Updating each property for background job <{HiveLog.Job.Id}> using query <{query}>", backgroundJobId);

            foreach (var property in properties)
            {
                _logger.Debug($"Updating property <{property.Name}> for background job <{HiveLog.Job.Id}>", backgroundJobId);
                var parameters = property.ToUpdateParameters(null);
                parameters.AddBackgroundJobId(backgroundJobId, nameof(backgroundJobId));

                var updated = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

                if (updated != 1) throw new InvalidOperationException($"Property <{property.Name}> for background job <{backgroundJobId}> was not updated");
                _logger.Debug($"Updated property <{property.Name}> for background job <{HiveLog.Job.Id}>", backgroundJobId);
            }

            _logger.Log($"Updated <{properties.Length}> properties for background job <{HiveLog.Job.Id}>", backgroundJobId);
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
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Deleting <{properties.Length}> properties for background job <{HiveLog.Job.Id}>", backgroundJobId);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(DeletePropertiesAsync)}.{properties.Length}"), x =>
            {
                return x.Delete<BackgroundJobPropertyTable>().From(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable))
                        .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)).And
                                     .Column(c => c.Name).In.Parameters(properties.Select((x, i) => $"{nameof(properties)}{i}")));
            });
            _logger.Trace($"Deleting <{properties.Length}> properties for background job <{HiveLog.Job.Id}> using query <{query}>", backgroundJobId);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(backgroundJobId);
            properties.Execute((i, x) => parameters.AddPropertyName(x, $"{nameof(properties)}{i}"));

            var deleted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token));
            if (deleted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be deleted but only <{deleted}> were deleted");
            _logger.Log($"Deleting <{deleted}> properties for background job <{HiveLog.Job.Id}>", backgroundJobId);
        }

        /// <inheritdoc/>
        public virtual async Task<BackgroundJobStorageData> GetBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));

            var backgroundJob = (await GetBackgroundJobsByIdsAsync(connection, id.ConvertTo<long>().AsEnumerable(), token).ConfigureAwait(false)).FirstOrDefault();

            // Convert to storage format
            if (backgroundJob != null)
            {
                _logger.Log($"Selected background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
                return backgroundJob;
            }
            else
            {
                _logger.Log($"Could not select background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
                return null;
            }
        }
        /// <inheritdoc/>
        public virtual async Task<LockStorageData> TryLockBackgroundJobAsync(string id, string requester, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to set lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", id, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryLockBackgroundJobAsync)), x =>
            {
                var update = x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Set.Column(c => c.LockedBy).To.Parameter(nameof(requester))
                              .Set.Column(c => c.LockedAt).To.CurrentDate(DateType.Utc)
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester))));

                var select = x.Select<BackgroundJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                var updateIf = x.If().Condition(b => b.ExistsIn(x.Select<BackgroundJobTable>().Value(1).From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable)).Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))).ForUpdate()))
                                     .Then(update);

                return x.New().Append(updateIf).Append(select);
            });
            _logger.Trace($"Trying to set lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}> using query <{query}>", id, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(requester, nameof(requester));
            var lockState = await storageConnection.MySqlConnection.QuerySingleOrDefaultAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (lockState == null)
            {
                _logger.Log($"Could not lock background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}> because it does not exist", id, connection.Environment);
                return null;
            }

            _logger.Log($"Tried to set lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", id, connection.Environment);

            if (lockState.LockedBy != null)
            {
                return new LockStorageData()
                {
                    LockedBy = lockState.LockedBy,
                    LockedAtUtc = lockState.LockedAt,
                    LockHeartbeatUtc = lockState.LockHeartbeat
                }.ToUtc();
            }

            return new LockStorageData()
            {
            };
        }
        /// <inheritdoc/>
        public virtual async Task<LockStorageData> TryHeartbeatLockOnBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to set lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryHeartbeatLockOnBackgroundJobAsync)), x =>
            {
                var update = x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));

                var select = x.Select<BackgroundJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                return x.New().Append(update).Append(select);
            });
            _logger.Trace($"Trying to set lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var lockState = await storageConnection.MySqlConnection.QuerySingleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Tried to set lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);

            if (lockState.LockedBy != null)
            {
                return new LockStorageData()
                {
                    LockedBy = lockState.LockedBy,
                    LockedAtUtc = lockState.LockedAt,
                    LockHeartbeatUtc = lockState.LockHeartbeat
                }.ToUtc();
            }

            return new LockStorageData()
            {
            };
        }
        /// <inheritdoc/>
        public virtual async Task<bool> UnlockBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to remove lock from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UnlockBackgroundJobAsync)), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying remove lock from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed lock from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
                return true;
            }
            else
            {
                _logger.Warning($"Could not remove lock from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{holder}>", id, connection.Environment, holder);
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

            _logger.Log($"Trying to remove locks from <{ids.Length}> background jobs in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UnlockBackgroundsJobAsync)}.{ids.Length}"), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).In.Parameters(jobIds.Select((x, i) => $"{nameof(jobIds)}{i}")).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying to remove locks from <{ids.Length}> background jobs in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddLocker(holder, nameof(holder));
            jobIds.Execute((i, x) => parameters.AddBackgroundJobId(x, $"{nameof(jobIds)}{i}"));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed locks from <{ids.Length}> background jobs of the total <{ids.Length}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", connection.Environment, holder);
            }
            else
            {
                _logger.Warning($"Could not remove any locks from the <{ids.Length}> background jobs in environment <{HiveLog.Environment}> for <{holder}>", connection.Environment);
            }
        }
        /// <inheritdoc/>
        public virtual async Task<(BackgroundJobStorageData[] Results, long Total)> SearchBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Selecting the next max <{pageSize}> background jobs from page <{page}> in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}>", storageConnection.Environment);

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

            _logger.Trace($"Selecting the next max <{pageSize}> background jobs from page <{page}> in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            var total = await reader.ReadSingleAsync<long>().ConfigureAwait(false);

            if(total <= 0)
            {
                _logger.Log($"No background jobs matching the supplied query condition", storageConnection.Environment);
                return (Array.Empty<BackgroundJobStorageData>(), 0);
            }

            Dictionary<long, (BackgroundJobTable Job, Dictionary<long, (BackgroundJobStateTable State, List<BackgroundJobStatePropertyTable> Properties)> States, List<BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (BackgroundJobTable Job, Dictionary<long, (BackgroundJobStateTable State, List<BackgroundJobStatePropertyTable> Properties)> States, List<BackgroundJobPropertyTable> Properties)>();

            // Mapping
            _ = reader.Read<BackgroundJobTable, BackgroundJobStateTable, BackgroundJobStatePropertyTable, BackgroundJobPropertyTable, Null>((b, s, sp, p) =>
            {
                // Job
                Dictionary<long, (BackgroundJobStateTable State, List<BackgroundJobStatePropertyTable> Properties)> states = null;
                List<BackgroundJobPropertyTable> properties = null;
                if (!backgroundJobs.TryGetValue(b.Id, out var backgroundJob))
                {
                    states = new Dictionary<long, (BackgroundJobStateTable State, List<BackgroundJobStatePropertyTable> Properties)>();
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
                if (!states.ContainsKey(s.Id)) states.Add(s.Id, (s, new List<BackgroundJobStatePropertyTable>()));

                // State property
                if (sp != null && !states[sp.StateId].Properties.Select(x => x.Name).Contains(sp.Name, StringComparer.OrdinalIgnoreCase)) states[sp.StateId].Properties.Add(sp);

                // Property
                if (p != null && (properties == null || !properties.Select(x => x.Name).Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    properties.Add(p);
                }

                return Null.Value;
            }, $"{nameof(BackgroundJobStateTable.Id)},{nameof(BackgroundJobStatePropertyTable.StateId)},{nameof(BackgroundJobPropertyTable.BackgroundJobId)}");

            // Convert to storage format
            List<BackgroundJobStorageData> jobStorageData = new List<BackgroundJobStorageData>();
            foreach (var backgroundJob in backgroundJobs)
            {
                var job = backgroundJob.Value.Job.ToStorageFormat(_hiveOptions.Get(connection.Environment), _cache);
                job.Lock = backgroundJob.Value.Job.ToLockStorageFormat();
                job.States = backgroundJob.Value.States.Values.Select(x =>
                {
                    var state = x.State.ToStorageFormat();
                    if (x.Properties.HasValue()) state.Properties = x.Properties.Select(p => p.ToStorageFormat()).ToList();
                    return state;
                }).ToList();
                if (backgroundJob.Value.Properties.HasValue()) job.Properties = backgroundJob.Value.Properties.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> because it matched the query condition <{queryConditions}>", job.Id, connection.Environment);
                jobStorageData.Add(job);
            }

            _logger.Log($"Selected <{jobStorageData.Count}> background jobs from page <{page}> in environment <{HiveLog.Environment}> out of the total <{total}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return (jobStorageData.ToArray(), total);
        }
        /// <inheritdoc/>
        public virtual async Task<long> CountBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Counting the amount of background jobs in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}>", storageConnection.Environment);

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

            _logger.Trace($"Counting the amount of background jobs in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var total = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Counted <{total}> background jobs in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return total;
        }
        /// <inheritdoc/>
        public virtual async Task<(BackgroundJobStorageData[] Results, long Total)> LockBackgroundJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Trying to lock the next <{limit}> background jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated lock query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedSearchAndLockQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildSearchAndLockQuery(_queryProvider, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }
            parameters.Add(nameof(limit), limit);
            parameters.Add(nameof(requester), requester);

            _logger.Trace($"Selecting the ids of the next <{limit}> background jobs in environment <{HiveLog.Environment}> to lock for <{requester}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            var total = await reader.ReadSingleAsync<long>().ConfigureAwait(false);

            if(total <= 0)
            {
                _logger.Log($"Locked no background jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);
                return (Array.Empty<BackgroundJobStorageData>(), total);
            }

            var ids = await reader.ReadAsync<long>().ConfigureAwait(false);

            if (!ids.HasValue())
            {
                _logger.Log($"Locked no background jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);
                return (Array.Empty<BackgroundJobStorageData>(), total);
            }

            // Update matching jobs
            _ = await UpdateBackgroundJobLocksByIdsAsync(connection, ids, requester, token).ConfigureAwait(false);

            // Select updated background jobs
            var jobStorageData = await GetBackgroundJobsByIdsAsync(connection, ids, token).ConfigureAwait(false);

            _logger.Log($"Locked <{jobStorageData.Length}> background jobs in environment <{HiveLog.Environment}> out of the total <{total}> for <{HiveLog.Job.LockHolder}> matching the query condition <{queryConditions}>", storageConnection.Environment, requester);
            return (jobStorageData.ToArray(), total);
        }
        private string BuildSearchAndLockQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            const string TotalParameter = "@Total";

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable));
            countQuery.Where(x =>
            {
                var builder = x.WhereGroup(x =>
                {
                    (joinProperty, joinState, joinStateProperty) = BuildWhereStatement<BackgroundJobTable, long, BackgroundJobPropertyTable, BackgroundJobStateTable, BackgroundJobStatePropertyTable>(x, parameters, queryConditions.Conditions, nameof(BackgroundJobPropertyTable.BackgroundJobId));
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
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobStateTable>(x => x.BackgroundJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(TableNames.BackgroundJobStatePropertyTable, typeof(BackgroundJobStatePropertyTable)).On(x => x.Column<BackgroundJobStateTable>(x => x.Id).EqualTo.Column<BackgroundJobStatePropertyTable>(x => x.StateId));

            // Select the ids to update because MariaDB update refuses to use the same index as selects and it rather wants to scan the whole table
            var selectIdQuery = countQuery.Clone().Column(x => x.Id).ForUpdateSkipLocked().Limit(x => x.Parameter(nameof(limit)));
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

            // Count total matching and assign to variable
            countQuery.ColumnExpression(x => x.AssignVariable(TotalParameter, x => x.Count(x => x.Id)));

            var selectIdIfQuery = queryProvider.If().Condition(x => x.Variable(TotalParameter).GreaterThan.Value(0))
                                                    .Then(selectIdQuery);

            // Determine what to update and keep an update lock
            return _queryProvider.New().Append(countQuery).Append(selectIdIfQuery).Build(_compileOptions);
        }
        private string BuildSearchQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            const string TotalParameter = "@Total";

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable));
            countQuery.Where(x =>
            {
                (joinProperty, joinState, joinStateProperty) = BuildWhereStatement<BackgroundJobTable, long, BackgroundJobPropertyTable, BackgroundJobStateTable, BackgroundJobStatePropertyTable>(x, parameters, queryConditions.Conditions, nameof(BackgroundJobPropertyTable.BackgroundJobId));
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobStateTable>(x => x.BackgroundJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(TableNames.BackgroundJobStatePropertyTable, typeof(BackgroundJobStatePropertyTable)).On(x => x.Column<BackgroundJobStateTable>(x => x.Id).EqualTo.Column<BackgroundJobStatePropertyTable>(x => x.StateId));

            // Select id of matching
            var selectIdQuery = countQuery.Clone().Column(x => x.Id).Limit(x => x.Parameter(nameof(page)), x => x.Parameter(nameof(pageSize)));
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
                                                                            .AllOf<BackgroundJobStatePropertyTable>()
                                                                            .AllOf<BackgroundJobPropertyTable>()
                                                                            .InnerJoin().Table("cte", 'c').On(x => x.Column(x => x.Id).EqualTo.Column('c', x => x.Id))
                                                                            .InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobStateTable>(c => c.BackgroundJobId))
                                                                            .LeftJoin().Table(TableNames.BackgroundJobStatePropertyTable, typeof(BackgroundJobStatePropertyTable)).On(x => x.Column<BackgroundJobStateTable>(c => c.Id).EqualTo.Column<BackgroundJobStatePropertyTable>(c => c.StateId))
                                                                            .LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                                                                            .OrderBy<BackgroundJobStateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                                                                            .OrderBy<BackgroundJobStateTable>(c => c.ElectedDate, SortOrders.Ascending));

            // Count total matching and assign to variable
            countQuery.ColumnExpression(x => x.AssignVariable(TotalParameter, x => x.Count(x => x.Id)));

            // Only select if count is larget than 0
            var selectIfQuery = queryProvider.If().Condition(x => x.Variable(TotalParameter).GreaterThan.Value(0))
                                                  .Then(selectIdQuery);

            return _queryProvider.New().Append(countQuery).Append(selectQuery).Build(_compileOptions);
        }
        private string BuildCountQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = queryProvider.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable));
            countQuery.Where(x =>
            {
                (joinProperty, joinState, joinStateProperty) = BuildWhereStatement<BackgroundJobTable, long, BackgroundJobPropertyTable, BackgroundJobStateTable, BackgroundJobStatePropertyTable>(x, parameters, queryConditions.Conditions, nameof(BackgroundJobPropertyTable.BackgroundJobId));
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobStateTable>(x => x.BackgroundJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(TableNames.BackgroundJobStatePropertyTable, typeof(BackgroundJobStatePropertyTable)).On(x => x.Column<BackgroundJobStateTable>(x => x.Id).EqualTo.Column<BackgroundJobStatePropertyTable>(x => x.StateId));

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
            _logger.Log($"Inserting <{count}> log entries for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(CreateBackgroundJobLogsAsync)}.{count}"), x =>
            {
                var insertQuery = x.Insert<BackgroundJobLogTable>().Into(table: TableNames.BackgroundJobLogTable).ColumnsOf(nameof(BackgroundJobLogTable.CreatedAt));
                logEntries.Execute((i, x) =>
                {
                    insertQuery.Values(x =>  x.Parameter(p => p.BackgroundJobId, i)
                                      , x => x.Parameter(p => p.LogLevel, i)
                                      , x => x.Parameter(p => p.Message, i)
                                      , x => x.Parameter(p => p.ExceptionType, i)
                                      , x => x.Parameter(p => p.ExceptionMessage, i)
                                      , x => x.Parameter(p => p.ExceptionStackTrace, i)
                                      , x => x.Parameter(p => p.CreatedAtUtc, i));
                });
                return insertQuery;
            });
            _logger.Trace($"Inserting <{count}> log entries for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            logEntries.Execute((i, x) => new BackgroundJobLogTable(backgroundJobId, x).AppendCreateParameters(parameters, i));

            var inserted = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Inserted <{inserted}> log entries for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<LogEntry[]> GetBackgroundJobLogsAsync(IStorageConnection connection, string id, LogLevel[] logLevels, int page, int pageSize, bool mostRecentFirst, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            page.ValidateArgumentLarger(nameof(page), 0);
            pageSize.ValidateArgumentLarger(nameof(pageSize), 1);
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Fetching up to <{pageSize}> logs from page <{page}> of background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetBackgroundJobLogsAsync)}.{logLevels?.Length ?? 0}.{mostRecentFirst}"), x =>
            {
                var getQuery = x.Select<BackgroundJobLogTable>().All()
                                .From()
                                .Limit(SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(page)), SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(pageSize)))
                                .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(id)))
                                .OrderBy(x => x.CreatedAtUtc, mostRecentFirst ? SortOrders.Descending : SortOrders.Ascending);

                if (logLevels.HasValue()) getQuery.Where(x => x.Column(x => x.LogLevel).In.Parameters(logLevels.Select((i, x) => $"{nameof(logLevels)}{i}")));
                return getQuery;
            });
            _logger.Trace($"Fetching up to <{pageSize}> logs from page <{page}> of background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>());
            parameters.AddPage(page, pageSize);
            parameters.AddPageSize(pageSize);
            if (logLevels.HasValue()) logLevels.Execute((i, x) => parameters.Add($"{nameof(logLevels)}{i}", x, DbType.Int32, ParameterDirection.Input));

            var logs = (await storageConnection.MySqlConnection.QueryAsync<LogEntry>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Fetched <{logs.Length}> logs from page <{page}> of background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            return logs;
        }
        /// <inheritdoc/>
        public virtual async Task<(bool Exists, string Data)> TryGetBackgroundJobDataAsync(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to get data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryGetBackgroundJobDataAsync)), x =>
            {
                return x.Select<BackgroundJobDataTable>().Column(c => c.Value)
                        .From()
                        .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(id))
                                 .And.Column(c => c.Name).EqualTo.Parameter(nameof(name)));
            });
            _logger.Trace($"Trying to get data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddDataName(name);

            var value = await storageConnection.MySqlConnection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Fetched data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>: {value ?? "NULL"}", id, storageConnection.Environment);
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
            _logger.Log($"Saving data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
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
            _logger.Trace($"Saving data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var table = new BackgroundJobDataTable()
            {
                BackgroundJobId = id.ConvertTo<long>(),
                Name = name,
                Value = value
            };
            var parameters = table.ToCreateParameters();
            await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<BackgroundJobStorageData[]> GetTimedOutBackgroundJobs(IStorageConnection connection, int limit, string requester, TimeSpan timeoutThreshold, CancellationToken token = default)
        {
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            // Get ids to update
            _logger.Log($"Selecting at most <{limit}> background jobs where the lock timed out in environment <{HiveLog.Environment}> for <{requester}> with update lock", connection.Environment);
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
            _logger.Trace($"Selecting at most <{limit}> background jobs where the lock timed out in environment <{HiveLog.Environment}> for <{requester}> with update lock using query <{selectIdQuery}>", connection.Environment);

            var parameters = new DynamicParameters();
            parameters.AddLimit(limit);
            parameters.Add(nameof(timeoutThreshold), -timeoutThreshold.TotalMilliseconds, DbType.Double, ParameterDirection.Input);
            var ids = (await storageConnection.MySqlConnection.QueryAsync<long>(new CommandDefinition(selectIdQuery, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            if (!ids.HasValue())
            {
                _logger.Log($"No timed out background jobs in environment <{HiveLog.Environment}> locked for <{requester}>", connection.Environment);
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

            _logger.Log($"Selecting <{ids.GetCount()}> background jobs by id in environment <{HiveLog.Environment}>", storageConnection.Environment);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetBackgroundJobsByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return x.Select<BackgroundJobTable>().From(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .AllOf<BackgroundJobTable>()
                        .AllOf<BackgroundJobStateTable>()
                        .AllOf<BackgroundJobStatePropertyTable>()
                        .AllOf<BackgroundJobPropertyTable>()
                        .InnerJoin().Table(TableNames.BackgroundJobStateTable, typeof(BackgroundJobStateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobStateTable>(c => c.BackgroundJobId))
                        .LeftJoin().Table(TableNames.BackgroundJobStatePropertyTable, typeof(BackgroundJobStatePropertyTable)).On(x => x.Column<BackgroundJobStateTable>(c => c.Id).EqualTo.Column<BackgroundJobStatePropertyTable>(c => c.StateId))
                        .LeftJoin().Table(TableNames.BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                        .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")))
                        .OrderBy<BackgroundJobStateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                        .OrderBy<BackgroundJobStateTable>(c => c.ElectedDate, SortOrders.Ascending);
            });
            _logger.Trace($"Selecting <{ids.GetCount()}> background jobs by id in environment <{HiveLog.Environment}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            ids.Execute((i, x) => parameters.AddBackgroundJobId(x, $"Id{i}"));
            Dictionary<long, (BackgroundJobTable Job, Dictionary<long, (BackgroundJobStateTable State, Dictionary<string, BackgroundJobStatePropertyTable> Properties)> States, Dictionary<string, BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (BackgroundJobTable Job, Dictionary<long, (BackgroundJobStateTable State, Dictionary<string, BackgroundJobStatePropertyTable> Properties)> States, Dictionary<string, BackgroundJobPropertyTable> Properties)>();
            _ = await storageConnection.MySqlConnection.QueryAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token), (BackgroundJobTable b, BackgroundJobStateTable s, BackgroundJobStatePropertyTable sp, BackgroundJobPropertyTable p) =>
            {
                // Job
                Dictionary<long, (BackgroundJobStateTable State, Dictionary<string, BackgroundJobStatePropertyTable> Properties)> states = null;
                Dictionary<string, BackgroundJobPropertyTable> properties = null;
                if (!backgroundJobs.TryGetValue(b.Id, out var backgroundJob))
                {
                    states = new Dictionary<long, (BackgroundJobStateTable State, Dictionary<string, BackgroundJobStatePropertyTable> Properties)>();
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
                if (!states.ContainsKey(s.Id)) states.Add(s.Id, (s, new Dictionary<string, BackgroundJobStatePropertyTable>(StringComparer.OrdinalIgnoreCase)));

                // State property
                if (sp != null && !states[sp.StateId].Properties.ContainsKey(sp.Name)) states[sp.StateId].Properties.Add(sp.Name, sp);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(BackgroundJobStateTable.Id)},{nameof(BackgroundJobStatePropertyTable.StateId)},{nameof(BackgroundJobPropertyTable.BackgroundJobId)}").ConfigureAwait(false);

            // Convert to storage format
            List<BackgroundJobStorageData> jobStorageData = new List<BackgroundJobStorageData>();

            foreach (var backgroundJob in backgroundJobs)
            {
                var job = backgroundJob.Value.Job.ToStorageFormat(_hiveOptions.Get(connection.Environment), _cache);
                job.Lock = backgroundJob.Value.Job.ToLockStorageFormat();
                job.States = backgroundJob.Value.States.Values.Select(x =>
                {
                    var state = x.State.ToStorageFormat();
                    if (x.Properties.HasValue()) state.Properties = x.Properties.Values.Select(p => p.ToStorageFormat()).ToList();
                    return state;
                }).ToList();
                if (backgroundJob.Value.Properties.HasValue()) job.Properties = backgroundJob.Value.Properties.Values.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, connection.Environment);
                jobStorageData.Add(job);
            }

            return jobStorageData.ToArray();
        }

        private async Task<int> UpdateBackgroundJobLocksByIdsAsync(IStorageConnection connection, IEnumerable<long> ids, string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));

            // Generate query
            _logger.Log($"Updating <{ids.GetCount()}> background jobs locks by id in environment <{HiveLog.Environment}> so they are held by <{holder}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UpdateBackgroundJobLocksByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return x.Update<BackgroundJobTable>().Table(TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(x => x.LockedBy).To.Parameter(nameof(holder))
                        .Set.Column(x => x.LockHeartbeat).To.CurrentDate(DateType.Utc)
                        .Set.Column(x => x.LockedAt).To.CurrentDate(DateType.Utc)
                        .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")));
            });
            _logger.Trace($"Updating <{ids.GetCount()}> background jobs locks by id in environment <{HiveLog.Environment}> so they are held by <{holder}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddLocker(holder, nameof(holder));
            ids.Execute((i, x) => parameters.AddBackgroundJobId(x, $"Id{i}"));

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Updated <{updated}> background jobs locks by id in environment <{HiveLog.Environment}> so they are now held by <{HiveLog.Job.LockHolder}>", storageConnection.Environment, holder);
            return updated;
        }

        /// <inheritdoc/>
        public virtual async Task<string[]> GetAllBackgroundJobQueuesAsync(IStorageConnection connection, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Selecting all distinct background job queues from environment <{HiveLog.Environment}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetAllBackgroundJobQueuesAsync)), x =>
            {
                return x.Select<BackgroundJobTable>()
                            .Distinct().Column(x => x.Queue)
                        .From(table: TableNames.BackgroundJobTable, typeof(BackgroundJobTable));
            });
            _logger.Trace($"Selecting all distinct background job queues from environment <{HiveLog.Environment}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var queues = (await storageConnection.MySqlConnection.QueryAsync<string>(new CommandDefinition(query, null, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Selected <{queues.Length}> distinct background job queues from environment <{HiveLog.Environment}>", storageConnection.Environment);
            return queues;
        }

        /// <inheritdoc/>
        public virtual async Task<bool> TryDeleteBackgroundJobAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Deleting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> if it is still held by <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryDeleteBackgroundJobAsync)), x =>
            {
                return x.Delete<BackgroundJobTable>().From(table: TableNames.BackgroundJobTable, typeof(BackgroundJobTable))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(id)).And
                                     .Column(x => x.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Deleting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> if it is still held by <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(id.ConvertTo<long>(), nameof(id));
            parameters.AddLocker(holder, nameof(holder));

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Deletion of background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment} held by <{HiveLog.Job.LockHolder}> was <{wasDeleted}>", id, connection.Environment, holder);

            return wasDeleted;
        }
        /// <inheritdoc/>
        public virtual async Task CreateBackgroundJobActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            action.ValidateArgument(nameof(action));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Creating new action on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", action.ComponentId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateBackgroundJobActionAsync)), x =>
            {
                return x.Insert<BackgroundJobActionTable>().Into(table: TableNames.BackgroundJobActionTable).Columns(x => x.BackgroundJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAtUtc)
                        .Parameters(x => x.BackgroundJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAtUtc);
            });
            _logger.Trace($"Creating new action on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", action.ComponentId, connection.Environment);

            // Execute query
            var backgroundJobAction = new BackgroundJobActionTable(action, _hiveOptions.Get(_environment), _cache);
            var parameters = backgroundJobAction.ToCreateParameters();

            _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Created new action on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", action.ComponentId, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<ActionInfo[]> GetNextBackgroundJobActionsAsync(IStorageConnection connection, string id, int limit, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var backgroundJobId = id.ConvertTo<long>();
            _logger.Log($"Fetching the next <{limit}> pending actions on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", backgroundJobId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetNextBackgroundJobActionsAsync)), x =>
            {
                return x.Select<BackgroundJobActionTable>().All().From(TableNames.BackgroundJobActionTable, typeof(BackgroundJobActionTable))
                       .Where(x => x.Column(x => x.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId))) 
                       .OrderBy(x => x.Priority, SortOrders.Ascending).OrderBy(x => x.CreatedAtUtc, SortOrders.Ascending)
                       .Limit(x => x.Parameter(nameof(limit)));
            });
            _logger.Trace($"Fetching the next <{limit}> pending actions on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", backgroundJobId, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddBackgroundJobId(backgroundJobId);
            parameters.AddLimit(limit);
            var actions = (await storageConnection.MySqlConnection.QueryAsync<BackgroundJobActionTable>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Fetched <{actions?.Length}> pending actions on background job <{ HiveLog.Job.Id}> in environment <{ HiveLog.Environment}> ", backgroundJobId, connection.Environment);
            return actions.Select(x => x.ToAction(_hiveOptions.Get(_environment), _cache)).ToArray();
        }
        /// <inheritdoc/>
        public virtual async Task<bool> DeleteBackgroundJobActionByIdAsync(IStorageConnection connection, string id, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var actionId = id.ConvertTo<long>();
            _logger.Log($"Removing background job action <{actionId}> in environment <{HiveLog.Environment}>", actionId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(DeleteBackgroundJobActionByIdAsync)), x =>
            {
                return x.Delete<BackgroundJobActionTable>().From(TableNames.BackgroundJobActionTable, typeof(BackgroundJobActionTable))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(actionId)));
            });
            _logger.Trace($"Removing background job action <{actionId}> in environment <{HiveLog.Environment}> using query <{query}>", actionId, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(actionId), actionId);

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Removing of background job action <{actionId}> in environment <{HiveLog.Environment}> was <{wasDeleted}>", actionId, connection.Environment);
            return wasDeleted;
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
                       .AllOf<RecurringJobStatePropertyTable>()
                       .AllOf<RecurringJobPropertyTable>()
                       .From()
                       .LeftJoin().Table<RecurringJobPropertyTable>().On(x => x.Column(c => c.Id).EqualTo.Column<RecurringJobPropertyTable>(c => c.RecurringJobId))
                       .LeftJoin().Table<RecurringJobStateTable>().On(x => x.Column(c => c.Id).EqualTo.Column<RecurringJobStateTable>(c => c.RecurringJobId))
                       .LeftJoin().Table<RecurringJobStatePropertyTable>().On(x => x.Column<RecurringJobStateTable>(c => c.Id).EqualTo.Column<RecurringJobStatePropertyTable>(c => c.StateId))
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

            _logger.Log($"Inserting recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> if it does not exist yet", storageData.Id, _environment);
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
            _logger.Trace($"Inserting recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> if it does not exist yet using query <{query}>", _environment);

            var recurringJob = (await QueryRecurringJobs(storageConnection, new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).Single();

            if (recurringJob.States.HasValue())
            {
                _logger.Log($"Could not insert recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> because it already exists", storageData.Id, _environment);
            }
            else
            {
                _logger.Log($"Inserted recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", storageData.Id, _environment);
            }

            return recurringJob;
        }
        /// <inheritdoc/>
        public virtual async Task<RecurringJobStorageData> GetRecurringJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection, false);

            _logger.Log($"Selecting recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);

            var query = _queryProvider.GetQuery(nameof(GetRecurringJobAsync), x => SelectRecurringJobById(x));

            _logger.Log($"Selecting recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, connection.Environment);

            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            var recurringJob = (await QueryRecurringJobs(storageConnection, new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).FirstOrDefault();

            if (recurringJob != null)
            {
                _logger.Log($"Selected recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
                return recurringJob;
            }
            else
            {
                _logger.Log($"Could not select recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
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
            Dictionary<string, (RecurringJobTable Job, Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)> States, Dictionary<string, RecurringJobPropertyTable> Properties)> recurringJobs = new Dictionary<string, (RecurringJobTable Job, Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)> States, Dictionary<string, RecurringJobPropertyTable> Properties)>();
            _ = await storageConnection.MySqlConnection.QueryAsync(command, (RecurringJobTable b, RecurringJobStateTable s, RecurringJobStatePropertyTable sp, RecurringJobPropertyTable p) =>
            {
                // Job
                if (b == null) return Null.Value;
                Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)> states = null;
                Dictionary<string, RecurringJobPropertyTable> properties = null;
                if (!recurringJobs.TryGetValue(b.Id, out var recurringJob))
                {
                    states = new Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)>(0);
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
                if (s != null && !states.ContainsKey(s.Id)) states.Add(s.Id, (s, new Dictionary<string, RecurringJobStatePropertyTable>(StringComparer.OrdinalIgnoreCase)));

                // State property
                if (sp != null && !states[sp.StateId].Properties.ContainsKey(sp.Name)) states[sp.StateId].Properties.Add(sp.Name, sp);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(RecurringJobStateTable.Id)},{nameof(RecurringJobStatePropertyTable.StateId)},{nameof(RecurringJobPropertyTable.RecurringJobId)}").ConfigureAwait(false);

            // Convert to storage format
            List<RecurringJobStorageData> jobStorageData = new List<RecurringJobStorageData>();

            foreach (var recurringJob in recurringJobs)
            {
                var job = recurringJob.Value.Job.ToStorageFormat(_hiveOptions.Get(storageConnection.Environment), _cache);
                job.Lock = recurringJob.Value.Job.ToLockStorageFormat();
                job.States = recurringJob.Value.States.Values.Select(x =>
                {
                    var state = x.State.ToStorageFormat();
                    if (x.Properties.HasValue()) state.Properties = x.Properties.Values.Select(p => p.ToStorageFormat()).ToList();
                    return state;
                }).ToList();
                if (recurringJob.Value.Properties.HasValue()) job.Properties = recurringJob.Value.Properties.Values.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, storageConnection.Environment);
                jobStorageData.Add(job);
            }

            return jobStorageData.ToArray();
        }

        protected RecurringJobStorageData[] ReadRecurringJobs(SqlMapper.GridReader reader, string environment)
        {
            reader.ValidateArgument(nameof(reader));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            Dictionary<string, (RecurringJobTable Job, Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)> States, Dictionary<string, RecurringJobPropertyTable> Properties)> recurringJobs = new Dictionary<string, (RecurringJobTable Job, Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)> States, Dictionary<string, RecurringJobPropertyTable> Properties)>();
            _ = reader.Read<RecurringJobTable, RecurringJobStateTable, RecurringJobStatePropertyTable, RecurringJobPropertyTable, Null>((RecurringJobTable b, RecurringJobStateTable s, RecurringJobStatePropertyTable sp, RecurringJobPropertyTable p) =>
            {
                // Job
                if (b == null) return Null.Value;
                Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)> states = null;
                Dictionary<string, RecurringJobPropertyTable> properties = null;
                if (!recurringJobs.TryGetValue(b.Id, out var recurringJob))
                {
                    states = new Dictionary<long, (RecurringJobStateTable State, Dictionary<string, RecurringJobStatePropertyTable> Properties)>(0);
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
                if (s != null && !states.ContainsKey(s.Id)) states.Add(s.Id, (s, new Dictionary<string, RecurringJobStatePropertyTable>(StringComparer.OrdinalIgnoreCase)));

                // State property
                if (sp != null && !states[sp.StateId].Properties.ContainsKey(sp.Name)) states[sp.StateId].Properties.Add(sp.Name, sp);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(RecurringJobStateTable.Id)},{nameof(RecurringJobStatePropertyTable.StateId)},{nameof(RecurringJobPropertyTable.RecurringJobId)}");

            // Convert to storage format
            List<RecurringJobStorageData> jobStorageData = new List<RecurringJobStorageData>();

            foreach (var recurringJob in recurringJobs)
            {
                var job = recurringJob.Value.Job.ToStorageFormat(_hiveOptions.Get(environment), _cache);
                job.Lock = recurringJob.Value.Job.ToLockStorageFormat();
                job.States = recurringJob.Value.States.Values.Select(x =>
                {
                    var state = x.State.ToStorageFormat();
                    if (x.Properties.HasValue()) state.Properties = x.Properties.Values.Select(p => p.ToStorageFormat()).ToList();
                    return state;
                }).ToList();
                if (recurringJob.Value.Properties.HasValue()) job.Properties = recurringJob.Value.Properties.Values.Select(x => x.ToStorageFormat()).ToList();

                _logger.Debug($"Selected recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, environment);
                jobStorageData.Add(job);
            }

            return jobStorageData.ToArray();
        }

        /// <inheritdoc/>
        public virtual async Task<bool> TryUpdateRecurringJobAsync(IStorageConnection connection, RecurringJobStorageData jobData, bool releaseLock, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Updating recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobData.Id, _environment);
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
            _logger.Trace($"Updating background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", jobData.Id, _environment);

            // Execute query
            var parameters = recurringJob.ToUpdateParameters(holder, releaseLock);
            parameters.AddLocker(holder, nameof(holder));

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated != 1)
            {
                _logger.Warning($"Could not update recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobData.Id, _environment);
                return false;
            }
            else
            {
                var recurringJobId = jobData.Id;
                // Persist new states
                if (jobData.ChangeTracker.NewStates.HasValue())
                {
                    var states = jobData.ChangeTracker.NewStates.Select(x => (new RecurringJobStateTable(x), x.Properties.Select(x => new RecurringJobStatePropertyTable(x)).ToArray())).ToArray();

                    await InsertStatesWithPropertiesAsync(storageConnection, recurringJobId, states, token).ConfigureAwait(false);
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
            _logger.Log($"Deleting recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> if it is still held by <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryDeleteBackgroundJobAsync)), x =>
            {
                return x.Delete<RecurringJobTable>().From()
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(nameof(id)).And
                                     .Column(x => x.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Deleting recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> if it is still held by <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(holder, nameof(holder));

            var wasDeleted = (await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)) == 1;

            _logger.Log($"Deletion of recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment} held by <{HiveLog.Job.LockHolder}> was <{wasDeleted}>", id, connection.Environment, holder);

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
        protected virtual async Task InsertStatesWithPropertiesAsync(MySqlStorageConnection connection, string recurringJobId, (RecurringJobStateTable State, RecurringJobStatePropertyTable[] Properties)[] states, CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);
            connection.ValidateArgument(nameof(connection));
            recurringJobId.ValidateArgumentNotNullOrWhitespace(nameof(recurringJobId));
            states.ValidateArgument(nameof(states));
            _logger.Log($"Inserting <{states.Length}> new states for background job <{HiveLog.Job.Id}>", recurringJobId);

            var parameters = new DynamicParameters();

            // Reset is current on existing states
            var resetQuery = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(InsertStatesWithPropertiesAsync)}.Reset"), x =>
            {
                return x.Update<RecurringJobStateTable>().Table()
                        .Set.Column(c => c.IsCurrent).To.Value(false)
                        .Where(w => w.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(recurringJobId)))
                        .Build(_compileOptions);
            });

            parameters.AddRecurringJobId(recurringJobId);
            _logger.Trace($"Resetting {nameof(BackgroundJobStateTable.IsCurrent)} to false for existing states for recurring job <{HiveLog.Job.Id}> using query <{resetQuery}>", recurringJobId);
            await connection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(resetQuery, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            // Insert new
            states.Last().State.IsCurrent = true;
            foreach (var (state, properties) in states)
            {
                state.RecurringJobId = recurringJobId;
                state.CreatedAt = DateTime.UtcNow;
                state.ModifiedAt = DateTime.UtcNow;

                var propertyCount = properties?.Length ?? 0;

                // Insert state with it's properties
                _logger.Debug($"Inserting state <{HiveLog.Job.State}> for recurring job <{HiveLog.Job.Id}> with <{propertyCount}> properties", state.Name, recurringJobId);
                parameters = state.ToCreateParameters();
                var query = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(InsertStatesWithPropertiesAsync)}.Insert.{propertyCount}"), x =>
                {
                    var insert = x.Insert<RecurringJobStateTable>().Into()
                                  .Columns(c => c.Name, c => c.OriginalType, c => c.RecurringJobId, c => c.Sequence, c => c.ElectedDate, c => c.Reason, c => c.IsCurrent, c => c.CreatedAt)
                                  .Parameters(c => c.Name, c => c.OriginalType, c => c.RecurringJobId, c => c.Sequence, c => c.ElectedDate, c => c.Reason, c => c.IsCurrent, c => c.CreatedAt);

                    var select = x.Select().LastInsertedId();

                    var query = x.New().Append(insert).Append(select);
                    if (propertyCount > 0)
                    {
                        var insertProperties = x.Insert<RecurringJobStatePropertyTable>().Into()
                                            .Columns(c => c.StateId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue);
                        Enumerable.Range(0, propertyCount).Execute(i =>
                        {
                            insertProperties.Values(x => x.LastInsertedId(),
                                                    x => x.Parameter(x => x.Name, i),
                                                    x => x.Parameter(x => x.Type, i),
                                                    x => x.Parameter(x => x.OriginalType, i),
                                                    x => x.Parameter(x => x.TextValue, i),
                                                    x => x.Parameter(x => x.NumberValue, i),
                                                    x => x.Parameter(x => x.FloatingNumberValue, i),
                                                    x => x.Parameter(x => x.DateValue, i),
                                                    x => x.Parameter(x => x.BooleanValue, i),
                                                    x => x.Parameter(x => x.OtherValue, i));
                        });

                        query.Append(insertProperties);
                    }

                    return query;
                });
                properties.Execute((i, x) => x.AppendCreateParameters(parameters, i));
                _logger.Trace($"Inserting state <{HiveLog.Job.State}> for recurring job <{HiveLog.Job.Id}> with <{propertyCount}> properties using query <{query}>", state.Name, recurringJobId);

                state.Id = await connection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Debug($"Inserted state <{HiveLog.Job.State}> for recurring job <{HiveLog.Job.Id}> with id <{state.Id}> with <{propertyCount}> properties", state.Name, recurringJobId);
                properties.Execute(x => x.StateId = state.Id);
            }

            _logger.Log($"Inserted <{states.Length}> new states for recurring job <{HiveLog.Job.Id}>", recurringJobId);
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

            _logger.Log($"Inserting <{properties.Length}> new properties for recurring job <{HiveLog.Job.Id}>", recurringJobId);
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
                Enumerable.Range(0, properties.Length).Execute(x => insertQuery.Parameters(x, c => c.RecurringJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.BooleanValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt));
                return insertQuery;
            });
            _logger.Trace($"Inserting <{properties.Length}> properties for recurring job <{HiveLog.Job.Id}> using query <{query}>", recurringJobId);

            properties.Execute((i, x) => x.AppendCreateParameters(parameters, i));
            var inserted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            if (inserted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be inserted but only <{inserted}> were inserted");
            _logger.Log($"Inserted <{inserted}> new properties for recurring job <{HiveLog.Job.Id}>", recurringJobId);
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

            _logger.Log($"Updating <{properties.Length}> properties for recurring job <{HiveLog.Job.Id}>", recurringJobId);
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
            _logger.Trace($"Updating each property for recurring job <{HiveLog.Job.Id}> using query <{query}>", recurringJobId);

            foreach (var property in properties)
            {
                _logger.Debug($"Updating property <{property.Name}> for recurring job <{HiveLog.Job.Id}>", recurringJobId);
                var parameters = property.ToUpdateParameters(null);
                parameters.AddRecurringJobId(recurringJobId);

                var updated = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

                if (updated != 1) throw new InvalidOperationException($"Property <{property.Name}> for recurring job <{recurringJobId}> was not updated");
                _logger.Debug($"Updated property <{property.Name}> for recurring job <{HiveLog.Job.Id}>", recurringJobId);
            }

            _logger.Log($"Updated <{properties.Length}> properties for recurring job <{HiveLog.Job.Id}>", recurringJobId);
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

            _logger.Log($"Deleting <{properties.Length}> properties for recurring job <{HiveLog.Job.Id}>", recurringJobId);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"Recurring.{nameof(DeletePropertiesAsync)}.{properties.Length}"), x =>
            {
                return x.Delete<RecurringJobPropertyTable>().From()
                        .Where(x => x.Column(c => c.RecurringJobId).EqualTo.Parameter(nameof(recurringJobId)).And
                                     .Column(c => c.Name).In.Parameters(properties.Select((x, i) => $"{nameof(properties)}{i}")));
            });
            _logger.Trace($"Deleting <{properties.Length}> properties for recurring job <{HiveLog.Job.Id}> using query <{query}>", recurringJobId);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(recurringJobId);
            properties.Execute((i, x) => parameters.AddPropertyName(x, $"{nameof(properties)}{i}"));

            var deleted = await connection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, connection.MySqlTransaction, cancellationToken: token));
            if (deleted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be deleted but only <{deleted}> were deleted");
            _logger.Log($"Deleting <{deleted}> properties for recurring job <{HiveLog.Job.Id}>", recurringJobId);
        }
        /// <inheritdoc/>
        public virtual async Task CreateRecurringJobActionAsync(IStorageConnection connection, ActionInfo action, CancellationToken token = default)
        {
            action.ValidateArgument(nameof(action));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Creating new action on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", action.ComponentId, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateRecurringJobActionAsync)), x =>
            {
                return x.Insert<RecurringJobActionTable>().Into(table: TableNames.BackgroundJobActionTable).Columns(x => x.RecurringJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAtUtc)
                        .Parameters(x => x.RecurringJobId, x => x.Type, x => x.ContextType, x => x.Context, x => x.ExecutionId, x => x.ForceExecute, x => x.Priority, x => x.CreatedAtUtc);
            });
            _logger.Trace($"Creating new action on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", action.ComponentId, connection.Environment);

            // Execute query
            var recurringJobAction = new RecurringJobActionTable(action, _hiveOptions.Get(_environment), _cache);
            var parameters = recurringJobAction.ToCreateParameters();

            _ = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Created new action on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", action.ComponentId, connection.Environment);
        }
        /// <inheritdoc/>
        public virtual async Task<LockStorageData> TryLockRecurringJobAsync(IStorageConnection connection, string id, string requester, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            // Generate query
            _logger.Log($"Trying to set lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", id, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryLockBackgroundJobAsync)), x =>
            {
                var update = x.Update<RecurringJobTable>().Table()
                              .Set.Column(c => c.LockedBy).To.Parameter(nameof(requester))
                              .Set.Column(c => c.LockedAt).To.CurrentDate(DateType.Utc)
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester))));

                var select = x.Select<RecurringJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From()
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                var updateIf = x.If().Condition(b => b.ExistsIn(x.Select<RecurringJobTable>().Value(1).From().Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))).ForUpdate()))
                                     .Then(update);

                return x.New().Append(updateIf).Append(select);
            });
            _logger.Trace($"Trying to set lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}> using query <{query}>", id, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(requester, nameof(requester));
            var lockState = await storageConnection.MySqlConnection.QuerySingleOrDefaultAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (lockState == null)
            {
                _logger.Log($"Could not lock recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}> because it does not exist", id, connection.Environment);
                return null;
            }

            _logger.Log($"Tried to set lock on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", id, connection.Environment);

            if (lockState.LockedBy != null)
            {
                return new LockStorageData()
                {
                    LockedBy = lockState.LockedBy,
                    LockedAtUtc = lockState.LockedAt,
                    LockHeartbeatUtc = lockState.LockHeartbeat
                }.ToUtc();
            }

            return new LockStorageData()
            {
            };
        }
        /// <inheritdoc/>
        public virtual async Task<LockStorageData> TryHeartbeatLockOnRecurringJobAsync(IStorageConnection connection, string id, string holder, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to set lock heartbeat on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryHeartbeatLockOnRecurringJobAsync)), x =>
            {
                var update = x.Update<RecurringJobTable>().Table()
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));

                var select = x.Select<RecurringJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From()
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                return x.New().Append(update).Append(select);
            });
            _logger.Trace($"Trying to set lock heartbeat on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var lockState = await storageConnection.MySqlConnection.QuerySingleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Tried to set lock heartbeat on recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);

            if (lockState.LockedBy != null)
            {
                return new LockStorageData()
                {
                    LockedBy = lockState.LockedBy,
                    LockedAtUtc = lockState.LockedAt,
                    LockHeartbeatUtc = lockState.LockHeartbeat
                }.ToUtc();
            }

            return new LockStorageData()
            {
            };
        }
        /// <inheritdoc/>
        public virtual async Task<bool> UnlockRecurringJobAsync(IStorageConnection connection, string id, string holder, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to remove lock from recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UnlockRecurringJobAsync)), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying remove lock from recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddRecurringJobId(id, nameof(id));
            parameters.AddLocker(holder, nameof(holder));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed lock from recurring job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
                return true;
            }
            else
            {
                _logger.Warning($"Could not remove lock from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{holder}>", id, connection.Environment, holder);
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

            _logger.Log($"Trying to remove locks from <{ids.Length}> recurring jobs in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UnlockRecurringJobsAsync)}.{ids.Length}"), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).In.Parameters(ids.Select((x, i) => $"{nameof(ids)}{i}")).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying to remove locks from <{ids.Length}> recurring jobs in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();

            parameters.AddLocker(holder, nameof(holder));
            ids.Execute((i, x) => parameters.AddRecurringJobId(x, $"{nameof(ids)}{i}"));
            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);

            if (updated > 0)
            {
                _logger.Log($"Removed locks from <{ids.Length}> recurring jobs of the total <{ids.Length}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", connection.Environment, holder);
            }
            else
            {
                _logger.Warning($"Could not remove any locks from the <{ids.Length}> recurring jobs in environment <{HiveLog.Environment}> for <{holder}>", connection.Environment);
            }
        }
        /// <inheritdoc/>
        public virtual async Task<(RecurringJobStorageData[] Results, long Total)> SearchRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int pageSize, int page, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Selecting the next max <{pageSize}> recurring jobs from page <{page}> in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}>", storageConnection.Environment);

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

            _logger.Trace($"Selecting the next max <{pageSize}> recurring jobs from page <{page}> in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            var total = await reader.ReadSingleAsync<long>().ConfigureAwait(false);

            if (total <= 0)
            {
                _logger.Log($"No recurring jobs matching the supplied query condition", storageConnection.Environment);
                return (Array.Empty<RecurringJobStorageData>(), 0);
            }

            Dictionary<long, (BackgroundJobTable Job, Dictionary<long, (BackgroundJobStateTable State, List<BackgroundJobStatePropertyTable> Properties)> States, List<BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (BackgroundJobTable Job, Dictionary<long, (BackgroundJobStateTable State, List<BackgroundJobStatePropertyTable> Properties)> States, List<BackgroundJobPropertyTable> Properties)>();

            var jobStorageData = ReadRecurringJobs(reader, storageConnection.Environment);

            _logger.Log($"Selected <{jobStorageData.Length}> recurring jobs from page <{page}> in environment <{HiveLog.Environment}> out of the total <{total}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return (jobStorageData.ToArray(), total);
        }
        /// <inheritdoc/>
        public virtual async Task<long> CountRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Counting the amount of recurring jobs in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}>", storageConnection.Environment);

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

            _logger.Trace($"Counting the amount of recurring jobs in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            //// Execute query
            var total = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Counted <{total}> recurring jobs in environment <{HiveLog.Environment}> matching the query condition <{queryConditions}>", storageConnection.Environment);
            return total;
        }
        /// <inheritdoc/>
        public virtual async Task<(RecurringJobStorageData[] Results, long Total)> LockRecurringJobsAsync(IStorageConnection connection, JobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection, true);

            _logger.Log($"Trying to lock the next <{limit}> recurring jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            string query;
            using (Helper.Time.CaptureDuration(x => _logger.LogMessage(_queryGenerationTraceLevel, $"Generated lock query <{queryConditions}> in {x.PrintTotalMs()}")))
            {
                bool generated = false;
                query = _queryProvider.GetQuery(GetCacheKey($"GeneratedRecurringJobSearchAndLockQuery.{queryConditions}"), x =>
                {
                    generated = true;
                    return BuildRecurringJobSearchAndLockQuery(_queryProvider, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, parameters);
                });

                if (!generated) AddParameters(queryConditions, parameters);
            }
            parameters.Add(nameof(limit), limit);
            parameters.Add(nameof(requester), requester);

            _logger.Trace($"Selecting the ids of the next <{limit}> recurring jobs in environment <{HiveLog.Environment}> to lock for <{requester}> matching the query condition <{queryConditions}> using query <{query}>", storageConnection.Environment);

            var reader = await storageConnection.MySqlConnection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            var total = await reader.ReadSingleAsync<long>().ConfigureAwait(false);

            if (total <= 0)
            {
                _logger.Log($"Locked no recurring jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);
                return (Array.Empty<RecurringJobStorageData>(), total);
            }

            var ids = await reader.ReadAsync<string>().ConfigureAwait(false);

            if (!ids.HasValue())
            {
                _logger.Log($"Locked no recurring jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query condition <{queryConditions}>", storageConnection.Environment);
                return (Array.Empty<RecurringJobStorageData>(), total);
            }

            // Update matching jobs
            _ = await UpdateRecurringJobLocksByIdsAsync(connection, ids, requester, token).ConfigureAwait(false);

            // Select updated background jobs
            var jobStorageData = await GetRecurringJobsByIdsAsync(connection, ids, token).ConfigureAwait(false);

            _logger.Log($"Locked <{jobStorageData.Length}> recurring jobs in environment <{HiveLog.Environment}> out of the total <{total}> for <{HiveLog.Job.LockHolder}> matching the query condition <{queryConditions}>", storageConnection.Environment, requester);
            return (jobStorageData.ToArray(), total);
        }
        /// <inheritdoc/>
        public virtual async Task<string[]> GetAllRecurringJobQueuesAsync(IStorageConnection connection, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Selecting all distinct recurring job queues from environment <{HiveLog.Environment}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetAllRecurringJobQueuesAsync)), x =>
            {
                return x.Select<RecurringJobTable>()
                            .Distinct().Column(x => x.Queue)
                        .From();
            });
            _logger.Trace($"Selecting all distinct recurring job queues from environment <{HiveLog.Environment}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var queues = (await storageConnection.MySqlConnection.QueryAsync<string>(new CommandDefinition(query, null, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

            _logger.Log($"Selected <{queues.Length}> distinct recurring job queues from environment <{HiveLog.Environment}>", storageConnection.Environment);
            return queues;
        }

        private async Task<int> UpdateRecurringJobLocksByIdsAsync(IStorageConnection connection, IEnumerable<string> ids, string holder, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));

            // Generate query
            _logger.Log($"Updating <{ids.GetCount()}> recurring jobs locks by id in environment <{HiveLog.Environment}> so they are held by <{holder}>", storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UpdateRecurringJobLocksByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return x.Update<RecurringJobTable>().Table()
                        .Set.Column(x => x.LockedBy).To.Parameter(nameof(holder))
                        .Set.Column(x => x.LockHeartbeat).To.CurrentDate(DateType.Utc)
                        .Set.Column(x => x.LockedAt).To.CurrentDate(DateType.Utc)
                        .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")));
            });
            _logger.Trace($"Updating <{ids.GetCount()}> recurring jobs locks by id in environment <{HiveLog.Environment}> so they are held by <{holder}> using query <{query}>", storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.AddLocker(holder, nameof(holder));
            ids.Execute((i, x) => parameters.AddRecurringJobId(x, $"Id{i}"));

            var updated = await storageConnection.MySqlConnection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Updated <{updated}> recurring jobs locks by id in environment <{HiveLog.Environment}> so they are now held by <{HiveLog.Job.LockHolder}>", storageConnection.Environment, holder);
            return updated;
        }

        private async Task<RecurringJobStorageData[]> GetRecurringJobsByIdsAsync(IStorageConnection connection, IEnumerable<string> ids, CancellationToken token = default)
        {
            var storageConnection = GetStorageConnection(connection);
            ids.ValidateArgumentNotNullOrEmpty(nameof(ids));

            _logger.Log($"Selecting <{ids.GetCount()}> recurring jobs by id in environment <{HiveLog.Environment}>", storageConnection.Environment);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(GetRecurringJobsByIdsAsync)}.{ids.GetCount()}"), x =>
            {
                return SelectRecurringJob(x)
                       .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"Id{i}")));
            });
            _logger.Trace($"Selecting <{ids.GetCount()}> background jobs by id in environment <{HiveLog.Environment}> using query <{query}>", storageConnection.Environment);

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

            const string TotalParameter = "@Total";

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = queryProvider.Select<RecurringJobTable>().From();
            countQuery.Where(x =>
            {
                var builder = x.WhereGroup(x =>
                {
                    (joinProperty, joinState, joinStateProperty) = BuildWhereStatement<RecurringJobTable, string, RecurringJobPropertyTable, RecurringJobStateTable, RecurringJobStatePropertyTable>(x, parameters, queryConditions.Conditions, nameof(RecurringJobPropertyTable.RecurringJobId));
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
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(TableNames.RecurringJobPropertyTable, typeof(RecurringJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobPropertyTable>(x => x.RecurringJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobStateTable>(x => x.RecurringJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(TableNames.RecurringJobStatePropertyTable, typeof(RecurringJobStatePropertyTable)).On(x => x.Column<RecurringJobStateTable>(x => x.Id).EqualTo.Column<RecurringJobStatePropertyTable>(x => x.StateId));

            // Select the ids to update because MariaDB update refuses to use the same index as selects and it rather wants to scan the whole table
            var selectIdQuery = countQuery.Clone().Column(x => x.Id).ForUpdateSkipLocked().Limit(x => x.Parameter(nameof(limit)));
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

            // Count total matching and assign to variable
            countQuery.ColumnExpression(x => x.AssignVariable(TotalParameter, x => x.Count(x => x.Id)));

            var selectIdIfQuery = queryProvider.If().Condition(x => x.Variable(TotalParameter).GreaterThan.Value(0))
                                                    .Then(selectIdQuery);

            // Determine what to update and keep an update lock
            return _queryProvider.New().Append(countQuery).Append(selectIdIfQuery).Build(_compileOptions);
        }
        private string BuildRecurringJobSearchQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, int pageSize, int page, QueryRecurringJobOrderByTarget? orderBy, bool orderByDescending, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            const string TotalParameter = "@Total";

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = queryProvider.Select<RecurringJobTable>().From();
            countQuery.Where(x =>
            {
                (joinProperty, joinState, joinStateProperty) = BuildWhereStatement<RecurringJobTable, string, RecurringJobPropertyTable, RecurringJobStateTable, RecurringJobStatePropertyTable>(x, parameters, queryConditions.Conditions, nameof(RecurringJobPropertyTable.RecurringJobId));
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(TableNames.RecurringJobPropertyTable, typeof(RecurringJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobPropertyTable>(x => x.RecurringJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobStateTable>(x => x.RecurringJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(TableNames.RecurringJobStatePropertyTable, typeof(RecurringJobStatePropertyTable)).On(x => x.Column<RecurringJobStateTable>(x => x.Id).EqualTo.Column<RecurringJobStatePropertyTable>(x => x.StateId));

            // Select id of matching
            var selectIdQuery = countQuery.Clone().Column(x => x.Id).Limit(x => x.Parameter(nameof(page)), x => x.Parameter(nameof(pageSize)));
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

            // Count total matching and assign to variable
            countQuery.ColumnExpression(x => x.AssignVariable(TotalParameter, x => x.Count(x => x.Id)));

            // Only select if count is larget than 0
            var selectIfQuery = queryProvider.If().Condition(x => x.Variable(TotalParameter).GreaterThan.Value(0))
                                                  .Then(selectIdQuery);

            return _queryProvider.New().Append(countQuery).Append(selectQuery).Build(_compileOptions);
        }
        private string BuildRecurringJobCountQuery(ISqlQueryProvider queryProvider, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryProvider.ValidateArgument(nameof(queryProvider));
            queryConditions.ValidateArgument(nameof(queryConditions));
            queryConditions.ValidateArgument(nameof(queryConditions));

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = queryProvider.Select<RecurringJobTable>().From();
            countQuery.Where(x =>
            {
                (joinProperty, joinState, joinStateProperty) = BuildWhereStatement<RecurringJobTable, string, RecurringJobPropertyTable, RecurringJobStateTable, RecurringJobStatePropertyTable>(x, parameters, queryConditions.Conditions, nameof(RecurringJobPropertyTable.RecurringJobId));
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(TableNames.RecurringJobPropertyTable, typeof(RecurringJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobPropertyTable>(x => x.RecurringJobId));
            if (joinState) countQuery.InnerJoin().Table(TableNames.RecurringJobStateTable, typeof(RecurringJobStateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<RecurringJobStateTable>(x => x.RecurringJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(TableNames.RecurringJobStatePropertyTable, typeof(RecurringJobStatePropertyTable)).On(x => x.Column<RecurringJobStateTable>(x => x.Id).EqualTo.Column<RecurringJobStatePropertyTable>(x => x.StateId));

            // Count total matching
            countQuery.Count(x => x.Id);

            return countQuery.Build(_compileOptions);
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
        private (bool requiresProperty, bool requiresState, bool requiresStateProperty) BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable>(IStatementConditionExpressionBuilder<TJobTable> builder, DynamicParameters parameters, IEnumerable<JobConditionGroupExpression> queryConditions, string foreignKeyColumnName)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
            where TJobStatePropertyTable : BaseStatePropertyTable
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            //// Try and determine if we can just build a query using joins on some tables
            // We can only join if they are all OR statements (exception for the last)
            var propertyConditions = GetConditions(queryConditions).Where(x => x.Condition.Target == QueryJobConditionTarget.Property).ToArray();
            bool canJoinProperty = propertyConditions.Take(propertyConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);

            // We can only join on state when they are all OR statements (exception for the last) unless they both target current and past states
            var stateConditions = GetConditions(queryConditions).Where(x => (x.Condition.CurrentStateComparison != null && x.Condition.CurrentStateComparison.Target != QueryJobStateConditionTarget.Property) || (x.Condition.PastStateComparison != null && x.Condition.PastStateComparison.Target != QueryJobStateConditionTarget.Property)).ToArray();
            bool onAnyState = stateConditions.Count(x => x.Condition.CurrentStateComparison != null) > 0 && stateConditions.Count(x => x.Condition.PastStateComparison != null) > 0;
            bool canJoinState = !onAnyState && stateConditions.Take(stateConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);

            // We can only join on state property when they are all OR statements (exception for the last) unless they both target current and past states
            bool canJoinStateProperty = false;
            if (canJoinState)
            {
                var statePropertyConditions = GetConditions(queryConditions).Where(x => (x.Condition.CurrentStateComparison != null && x.Condition.CurrentStateComparison.Target == QueryJobStateConditionTarget.Property) || (x.Condition.PastStateComparison != null && x.Condition.PastStateComparison.Target == QueryJobStateConditionTarget.Property)).ToArray();
                canJoinStateProperty = statePropertyConditions.Take(statePropertyConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);
            }

            var (requiresProperty, requiresState, requiresStateProperty) = BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable>(builder, parameters, queryConditions, foreignKeyColumnName, canJoinProperty, canJoinState, canJoinStateProperty);
            return (requiresProperty && canJoinProperty, requiresState && canJoinState, requiresStateProperty && canJoinStateProperty);
        }
        private (bool requiresProperty, bool requiresState, bool requiresStateProperty) BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable>(IStatementConditionExpressionBuilder<TJobTable> builder, DynamicParameters parameters, IEnumerable<JobConditionGroupExpression> queryConditions, string foreignKeyColumnName, bool canJoinProperty, bool canJoinState, bool canJoinStateProperty)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
            where TJobStatePropertyTable : BaseStatePropertyTable
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            bool requiresProperty = false;
            bool requiresState = false;
            bool requiresStateProperty = false;
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
                            var (conditionRequiresProperty, conditionRequiresState, conditionRequiresStateProperty) = BuildWhereStatement<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable>(x, parameters, expression.Group.Conditions, foreignKeyColumnName, canJoinProperty, canJoinState, canJoinStateProperty);

                            if (conditionRequiresProperty) requiresProperty = true;
                            if (conditionRequiresState) requiresState = true;
                            if (conditionRequiresStateProperty) requiresStateProperty = true;

                            return x.LastBuilder;
                        });
                    }
                    else
                    {
                        var (conditionRequiresProperty, conditionRequiresState, conditionRequiresStateProperty) = AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable>(builder, expression.Condition, parameters, foreignKeyColumnName, canJoinProperty, canJoinState, canJoinStateProperty);

                        if (conditionRequiresProperty) requiresProperty = true;
                        if (conditionRequiresState) requiresState = true;
                        if (conditionRequiresStateProperty) requiresStateProperty = true;
                    }

                    if (!isLast) builder.LastBuilder.AndOr(logicalOperator.HasValue && logicalOperator.Value == QueryLogicalOperator.Or ? LogicOperators.Or : LogicOperators.And);
                }
            }

            return (requiresProperty, requiresState, requiresStateProperty);
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
        private void AddComparison<TBuilder, TTable>(IStatementConditionExpressionBuilder<TBuilder> builder, JobPropertyCondition condition, DynamicParameters parameters)
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
        private (bool requiresProperty, bool requiresState, bool requiresStateProperty) AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable>(IStatementConditionExpressionBuilder<TJobTable> builder, JobCondition condition, DynamicParameters parameters, string foreignKeyColumnName, bool canJoinProperty, bool canJoinState, bool canJoinStateProperty)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
            where TJobStatePropertyTable : BaseStatePropertyTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresProperty = false;
            bool requiresState = false;
            bool requiresStateProperty = false;

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
                            var b = x.Column(typeof(TJobPropertyTable), foreignKeyColumnName).EqualTo.Column<TJobTable>(x => x.Id).And;
                            AddComparison<TJobPropertyTable, TJobPropertyTable>(b, condition.PropertyComparison, parameters);
                            return b.LastBuilder;
                        });
                        builder.ExistsIn(propertyBuilder);
                    }
                    else
                    {
                        requiresProperty = true;
                        AddComparison<TJobTable, TJobPropertyTable>(builder, condition.PropertyComparison, parameters);
                    }

                    break;
                case QueryJobConditionTarget.CurrentState:
                    requiresState = true;
                    requiresStateProperty = AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable, TJobTable>(builder, condition.CurrentStateComparison, parameters, foreignKeyColumnName, true, canJoinState, canJoinStateProperty);
                    break;
                case QueryJobConditionTarget.PastState:
                    requiresState = true;
                    requiresStateProperty = AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable, TJobTable>(builder, condition.PastStateComparison, parameters, foreignKeyColumnName, false, canJoinState, canJoinStateProperty);
                    break;
                default: throw new NotSupportedException($"Condition target <{condition.Target}> is not known");
            }

            return (requiresProperty, requiresState, requiresStateProperty);
        }

        private bool AddCondition<TJobTable, TId, TJobPropertyTable, TJobStateTable, TJobStatePropertyTable, T>(IStatementConditionExpressionBuilder<T> builder, JobStateCondition condition, DynamicParameters parameters, string foreignKeyColumnName, bool isCurrentState, bool canJoinState, bool canJoinStateProperty)
            where TJobTable : BaseJobTable<TId>
            where TJobPropertyTable : BasePropertyTable
            where TJobStateTable : BaseStateTable
            where TJobStatePropertyTable : BaseStatePropertyTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresProperty = false;

            if (canJoinState)
            {
                builder.WhereGroup(x =>
                {
                    _ = x.Column<TJobStateTable>(c => c.IsCurrent).EqualTo.Value(isCurrentState).And;

                    if (condition.Target == QueryJobStateConditionTarget.Property)
                    {
                        if (!canJoinStateProperty)
                        {
                            var subBuilder = _queryProvider.Select<TJobStatePropertyTable>().Value(1).From()
                                                            .Where(x =>
                                                            {
                                                                var b = x.Column(x => x.StateId).EqualTo.Column<TJobStateTable>(x => x.Id).And;
                                                                AddComparison<TJobStateTable, TJobStatePropertyTable, TJobStatePropertyTable>(b, condition, parameters);
                                                                return b.LastBuilder;
                                                            });

                            x.ExistsIn(subBuilder);
                            requiresProperty = false;
                            return x.LastBuilder;
                        }
                        else
                        {
                            requiresProperty = true;
                        }
                    }
                    AddComparison<TJobStateTable, TJobStatePropertyTable, T>(x, condition, parameters);
                    return x.LastBuilder;
                });
            }
            else
            {
                var subBuilder = _queryProvider.Select<TJobStateTable>().Value(1).From()
                                .Where(x =>
                                {
                                    var b = x.Column(typeof(TJobStateTable), foreignKeyColumnName).EqualTo.Column<TJobTable>(x => x.Id).And.Column(x => x.IsCurrent).EqualTo.Value(isCurrentState).And;
                                    AddComparison<TJobStateTable, TJobStatePropertyTable, TJobStateTable>(b, condition, parameters);
                                    return b.LastBuilder;
                                });
                if (condition.Target == QueryJobStateConditionTarget.Property) subBuilder.InnerJoin().Table<TJobStatePropertyTable>().On(x => x.Column(x => x.Id).EqualTo.Column<TJobStatePropertyTable>(x => x.StateId));
                builder.ExistsIn(subBuilder);
            }

            return requiresProperty;
        }
        private void AddComparison<TJobStateTable, TJobStatePropertyTable, T>(IStatementConditionExpressionBuilder<T> builder, JobStateCondition condition, DynamicParameters parameters)
            where TJobStateTable : BaseStateTable
            where TJobStatePropertyTable : BaseStatePropertyTable
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
                case QueryJobStateConditionTarget.Property:
                    AddComparison<T, TJobStatePropertyTable>(builder, condition.PropertyComparison, parameters);
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
                        AddParameters(condition.QueueComparison, queryConditions, parameters);
                        break;
                    case QueryJobConditionTarget.LockedBy:
                        AddParameters(condition.LockedByComparison, queryConditions, parameters);
                        break;
                    case QueryJobConditionTarget.Priority:
                        AddParameters(condition.PriorityComparison, queryConditions, parameters);
                        break;
                    case QueryJobConditionTarget.CreatedAt:
                        AddParameters(condition.CreatedAtComparison, queryConditions, parameters);
                        break;
                    case QueryJobConditionTarget.ModifiedAt:
                        AddParameters(condition.ModifiedAtComparison, queryConditions, parameters);
                        break;
                    case QueryJobConditionTarget.Property:
                        AddParameters(condition.PropertyComparison, queryConditions, parameters);
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
        private void AddParameters(QueryComparison queryComparison, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            queryComparison.ValidateArgument(nameof(queryComparison));
            queryConditions.ValidateArgument(nameof(queryConditions));
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
        private void AddParameters(JobPropertyCondition propertyCondition, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            propertyCondition.ValidateArgument(nameof(propertyCondition));
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
            parameters.Add(parameter, propertyCondition.Name);

            AddParameters(propertyCondition.Comparison, queryConditions, parameters);
        }
        private void AddParameters(JobStateCondition condition, JobQueryConditions queryConditions, DynamicParameters parameters)
        {
            condition.ValidateArgument(nameof(condition));
            queryConditions.ValidateArgument(nameof(queryConditions));
            parameters.ValidateArgument(nameof(parameters));

            switch (condition.Target)
            {
                case QueryJobStateConditionTarget.Name:
                    AddParameters(condition.NameComparison, queryConditions, parameters);
                    break;
                case QueryJobStateConditionTarget.Reason:
                    AddParameters(condition.ReasonComparison, queryConditions, parameters);
                    break;
                case QueryJobStateConditionTarget.ElectedDate:
                    AddParameters(condition.ElectedDateComparison, queryConditions, parameters);
                    break;
                case QueryJobStateConditionTarget.Property:
                    AddParameters(condition.PropertyComparison, queryConditions, parameters);
                    break;
            }
        }
        #endregion
    }
}
