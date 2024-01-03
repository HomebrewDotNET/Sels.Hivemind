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
using Sels.HiveMind.Storage.Sql.Job;
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
using Sels.HiveMind.Storage.MySql.Job;
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

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Persists HiveMind state in a MySql database.
    /// </summary>
    public class HiveMindMySqlStorage : IStorage
    {
        // Constants
        /// <summary>
        /// The name of the foreign key column towards the background job table.
        /// </summary>
        public const string BackgroundJobForeignKeyColumn = "BackgroundJobId";
        /// <summary>
        /// The name of the name column in the background job name table.
        /// </summary>
        public const string DataNameColumn = "Name";
        /// <summary>
        /// The name of the value column in the background job name table.
        /// </summary>
        public const string DataValueColumn = "Value";

        // Fields
        private readonly IOptionsSnapshot<HiveMindOptions> _hiveOptions;
        private readonly IMemoryCache _cache;
        private readonly HiveMindMySqlStorageOptions _options;
        private readonly ICachedSqlQueryProvider _queryProvider;
        private readonly string _environment;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        private readonly ExpressionCompileOptions _compileOptions = ExpressionCompileOptions.AppendSeparator;

        // Properties
        /// <summary>
        /// The name of the table that contains the background jobs.
        /// </summary>
        protected string BackgroundJobTable => $"HiveMind.{_environment}.BackgroundJob";
        /// <summary>
        /// The name of the table that contains the background job properties.
        /// </summary>
        protected string BackgroundJobPropertyTable => $"HiveMind.{_environment}.BackgroundJobProperty";
        /// <summary>
        /// The name of the table that contains the background job states.
        /// </summary>
        protected string BackgroundJobStateTable => $"HiveMind.{_environment}.BackgroundJobState";
        /// <summary>
        /// The name of the table that contains the background job state properties.
        /// </summary>
        protected string BackgroundJobStatePropertyTable => $"HiveMind.{_environment}.BackgroundJobStateProperty";
        /// <summary>
        /// The name of the table that contains the background job processing logs.
        /// </summary>
        protected string BackgroundJobLogTable => $"HiveMind.{_environment}.BackgroundJobLog";
        /// <summary>
        /// The name of the table that contains the processing data assigned to a job.
        /// </summary>
       protected string BackgroundJobDataTable => $"HiveMind.{_environment}.BackgroundJobData";

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="hiveMindOptions">The global hive mind options for this instance</param>
        /// <param name="cache">Optional cache used for type conversion</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, IMemoryCache cache, HiveMindMySqlStorageOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger logger = null)
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
                    aliasBuilder.SetAlias<MySqlBackgroundJobTable>("B");
                    aliasBuilder.SetAlias<BackgroundJobTable>("B");
                    aliasBuilder.SetAlias<BackgroundJobPropertyTable>("P");
                    aliasBuilder.SetAlias<StateTable>("S");
                    aliasBuilder.SetAlias<StatePropertyTable>("SP");
                }
            }));
            _logger = logger;
        }

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="options">The options for this instance</param>
        /// <param name="cache">Optional cache used for type conversion</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, IMemoryCache cache, HiveMindMySqlStorageOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger<HiveMindMySqlStorage> logger = null) : this(hiveMindOptions, cache, options, environment, connectionString, queryProvider, logger.CastToOrDefault<ILogger>())
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
        protected MySqlStorageConnection GetStorageConnection(IStorageConnection connection)
        {
            connection.ValidateArgument(nameof(connection));

            if (connection is MySqlStorageConnection storageConnection)
            {
                if (!storageConnection.Environment.EqualsNoCase(_environment)) throw new InvalidOperationException($"Storage connection was opened for environment <{storageConnection.Environment}> but storage is configured for <{_environment}>");

                return storageConnection;
            }

            throw new InvalidOperationException($"Expected connection to be of type <{typeof(MySqlStorageConnection)}> but got <{connection}>");
        }

        #region BackgroundJob
        /// <inheritdoc/>
        public virtual async Task<string> CreateBackgroundJobAsync(JobStorageData jobData, IStorageConnection connection, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Inserting new background job in environment <{HiveLog.Environment}>", _environment);
            // Job
            var job = new MySqlBackgroundJobTable(jobData, _hiveOptions.Get(connection.Environment), _cache);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateBackgroundJobAsync)), x =>
            {
                var insert = x.Insert<MySqlBackgroundJobTable>().Into(table: BackgroundJobTable)
                              .Columns(x => x.ExecutionId, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.CreatedAt, x => x.ModifiedAt)
                              .Parameters(x => x.ExecutionId, x => x.Queue, x => x.Priority, x => x.InvocationData, x => x.MiddlewareData, x => x.CreatedAt, x => x.ModifiedAt);
                var select = x.Select().LastInsertedId();

                return x.New().Append(insert).Append(select);
            });
            var parameters = new DynamicParameters();
            parameters.Add(nameof(job.ExecutionId), job.ExecutionId);
            parameters.Add(nameof(job.Queue), job.Queue);
            parameters.Add(nameof(job.Priority), job.Priority);
            parameters.Add(nameof(job.InvocationData), job.InvocationData);
            parameters.Add(nameof(job.MiddlewareData), job.MiddlewareData);
            parameters.Add(nameof(job.CreatedAt), job.CreatedAt);
            parameters.Add(nameof(job.ModifiedAt), job.ModifiedAt);
            _logger.Trace($"Inserting new background job in environment <{HiveLog.Environment}> using query <{query}>", _environment);

            var id = await storageConnection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            // States
            if (jobData.States.HasValue())
            {
                var states = jobData.States.Select(x => (new StateTable(x), x.Properties.Select(x => new StatePropertyTable(x)).ToArray())).ToArray();

                await InsertStatesAsync(storageConnection, id, states, token).ConfigureAwait(false);
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
        public virtual async Task<bool> UpdateBackgroundJobAsync(JobStorageData jobData, IStorageConnection connection, bool releaseLock, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Updating background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", jobData.Id, _environment);
            var holder = jobData.Lock.LockedBy;
            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UpdateBackgroundJobAsync)), x =>
            {
                return x.Update<MySqlBackgroundJobTable>().Table(BackgroundJobTable, typeof(MySqlBackgroundJobTable))
                        .Set.Column(c => c.Queue).To.Parameter(nameof(jobData.Queue))
                        .Set.Column(c => c.Priority).To.Parameter(nameof(jobData.Priority))
                        .Set.Column(c => c.ExecutionId).To.Parameter(nameof(jobData.ExecutionId))
                        .Set.Column(c => c.ModifiedAt).To.Parameter(nameof(jobData.ModifiedAtUtc))
                        .Set.Column(c => c.LockedBy).To.Parameter(nameof(jobData.Lock.LockedBy))
                        .Set.Column(c => c.LockedAt).To.Parameter(nameof(jobData.Lock.LockedAtUtc))
                        .Set.Column(c => c.LockHeartbeat).To.Parameter(nameof(jobData.Lock.LockHeartbeatUtc))
                        .Set.Column(c => c.LockProcessId).To.Case(x => x.When(x => x.Parameter(nameof(releaseLock)).EqualTo.Value(1))
                                                                            .Then.Null()
                                                                        .Else
                                                                            .Column(c => c.LockProcessId))
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(jobData.Id))
                                     .And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Updating background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", jobData.Id, _environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(jobData.Id), jobData.Id);
            parameters.Add(nameof(jobData.Queue), jobData.Queue);
            parameters.Add(nameof(jobData.Priority), jobData.Priority);
            parameters.Add(nameof(jobData.ExecutionId), jobData.ExecutionId);
            parameters.Add(nameof(jobData.ModifiedAtUtc), jobData.ModifiedAtUtc);
            parameters.Add(nameof(jobData.Lock.LockedBy), !releaseLock ? jobData.Lock.LockedBy : null);
            parameters.Add(nameof(jobData.Lock.LockedAtUtc), !releaseLock ? jobData.Lock.LockedAtUtc : (DateTime?)null);
            parameters.Add(nameof(jobData.Lock.LockHeartbeatUtc), !releaseLock ? jobData.Lock.LockHeartbeatUtc : (DateTime?)null);
            parameters.Add(nameof(releaseLock), releaseLock);
            parameters.Add(nameof(holder), holder);

            var updated = await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

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
                    var states = jobData.ChangeTracker.NewStates.Select(x => (new StateTable(x), x.Properties.Select(x => new StatePropertyTable(x)).ToArray())).ToArray();

                    await InsertStatesAsync(storageConnection, backgroundJobId, states, token).ConfigureAwait(false);
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
        /// <inheritdoc/>
        public virtual Task<bool> DeleteBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Inserts the new states for background job <paramref name="backgroundJobId"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="backgroundJobId">The id of the background job to insert the </param>
        /// <param name="states">The states and their properties to insert</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task InsertStatesAsync(MySqlStorageConnection connection, long backgroundJobId, (StateTable State, StatePropertyTable[] Properties)[] states, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            states.ValidateArgumentNotNullOrEmpty(nameof(states));

            _logger.Log($"Inserting <{states.Length}> new states for background job <{HiveLog.Job.Id}>", backgroundJobId);

            // Reset is current on existing states
            var resetQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(InsertStatesAsync)}.Reset"), x =>
            {
                return x.Update<StateTable>().Table(BackgroundJobStateTable, typeof(StateTable))
                        .Set.Column(c => c.IsCurrent).To.Value(false)
                        .Where(w => w.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)))
                        .Build(_compileOptions);
            });
            var parameters = new DynamicParameters();
            parameters.Add(nameof(backgroundJobId), backgroundJobId);
            _logger.Trace($"Resetting {nameof(StateTable.IsCurrent)} to false for existing states for background job <{HiveLog.Job.Id}> using query <{resetQuery}>", backgroundJobId);
            await connection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(resetQuery, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            // Insert new
            states.Last().State.IsCurrent = true;
            foreach (var (state, properties) in states)
            {
                state.BackgroundJobId = backgroundJobId;
                state.CreatedAt = DateTime.UtcNow;
                state.ModifiedAt = DateTime.UtcNow;

                // Insert state
                _logger.Debug($"Inserting state <{HiveLog.BackgroundJob.State}> for background job <{HiveLog.Job.Id}>", state.Name, backgroundJobId);
                parameters = new DynamicParameters();
                var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(InsertStatesAsync)}.State"), x =>
                {
                    var insert = x.Insert<StateTable>().Into(table: BackgroundJobStateTable)
                                    .Columns(c => c.Name, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.IsCurrent, c => c.CreatedAt)
                                    .Parameters(c => c.Name, c => c.OriginalType, c => c.BackgroundJobId, c => c.ElectedDate, c => c.Reason, c => c.IsCurrent, c => c.CreatedAt);

                    var select = x.Select().LastInsertedId();

                    return x.New().Append(insert).Append(select);
                });
                parameters.Add(nameof(state.Name), state.Name);
                parameters.Add(nameof(state.OriginalType), state.OriginalType);
                parameters.Add(nameof(state.BackgroundJobId), backgroundJobId);
                parameters.Add(nameof(state.ElectedDate), state.ElectedDate);
                parameters.Add(nameof(state.Reason), state.Reason);
                parameters.Add(nameof(state.IsCurrent), state.IsCurrent);
                parameters.Add(nameof(state.CreatedAt), DateTime.UtcNow);
                _logger.Trace($"Inserting state <{HiveLog.BackgroundJob.State}> for background job <{HiveLog.Job.Id}> using query <{query}>", state.Name, backgroundJobId);

                var stateId = await connection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Debug($"Inserted state <{HiveLog.BackgroundJob.State}> for background job <{HiveLog.Job.Id}> with id <{stateId}>", state.Name, backgroundJobId);

                // Insert properties
                if (properties.HasValue())
                {
                    properties.Execute(x => x.StateId = stateId);
                    await InsertStatePropertiesAsync(connection, properties, token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Inserted <{states.Length}> new states for background job <{HiveLog.Job.Id}>", backgroundJobId);
        }
        /// <summary>
        /// Inserts <paramref name="properties"/>.
        /// </summary>
        /// <param name="connection">The connection to use to execute the queries</param>
        /// <param name="properties">The properties to insert</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        protected virtual async Task InsertStatePropertiesAsync(MySqlStorageConnection connection, StatePropertyTable[] properties, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Inserting <{properties.Length}> new state properties");

            var parameters = new DynamicParameters();
            var query = _queryProvider.Insert<StatePropertyTable>().Into(table: BackgroundJobStatePropertyTable)
                                      .From(parameters, properties, c => c.StateId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.OtherValue)
                                      .Build(_compileOptions);
            _logger.Trace($"Inserting <{properties.Length}> new state properties using query <{query}>");

            var inserted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);
            if (inserted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be inserted but only <{inserted}> were inserted");
            _logger.Log($"Inserted {inserted} state properties");
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
            var query = _queryProvider.Insert<BackgroundJobPropertyTable>().Into(table: BackgroundJobPropertyTable)
                                      .From(parameters, properties, c => c.BackgroundJobId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.OtherValue, c => c.CreatedAt, c => c.ModifiedAt)
                                      .Build(_compileOptions);
            _logger.Trace($"Inserting <{properties.Length}> properties for background job <{HiveLog.Job.Id}> using query <{query}>", backgroundJobId);

            var inserted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);
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
                return x.Update<BackgroundJobPropertyTable>().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable))
                        .Set.Column(c => c.Type).To.Parameter(c => c.Type)
                        .Set.Column(c => c.OriginalType).To.Parameter(c => c.OriginalType)
                        .Set.Column(c => c.TextValue).To.Parameter(c => c.TextValue)
                        .Set.Column(c => c.NumberValue).To.Parameter(c => c.NumberValue)
                        .Set.Column(c => c.FloatingNumberValue).To.Parameter(c => c.FloatingNumberValue)
                        .Set.Column(c => c.DateValue).To.Parameter(c => c.DateValue)
                        .Set.Column(c => c.OtherValue).To.Parameter(c => c.OtherValue)
                        .Set.Column(c => c.ModifiedAt).To.Parameter(c => c.ModifiedAt)
                        .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId))
                                    .And.Column(c => c.Name).EqualTo.Parameter(c => c.Name));
            });
            _logger.Trace($"Updating each property for background job <{HiveLog.Job.Id}> using query <{query}>", backgroundJobId);

            foreach (var property in properties)
            {
                _logger.Debug($"Updating property <{property.Name}> for background job <{HiveLog.Job.Id}>", backgroundJobId);
                var parameters = new DynamicParameters();
                parameters.Add(nameof(backgroundJobId), backgroundJobId);
                parameters.Add(nameof(property.Name), property.Name);
                parameters.Add(nameof(property.Type), property.Type);
                parameters.Add(nameof(property.OriginalType), property.OriginalType);
                parameters.Add(nameof(property.TextValue), property.TextValue);
                parameters.Add(nameof(property.NumberValue), property.NumberValue);
                parameters.Add(nameof(property.FloatingNumberValue), property.FloatingNumberValue);
                parameters.Add(nameof(property.DateValue), property.DateValue);
                parameters.Add(nameof(property.OtherValue), property.OtherValue);
                parameters.Add(nameof(property.ModifiedAt), property.ModifiedAt);

                var updated = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);

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
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Deleting <{properties.Length}> properties for background job <{HiveLog.Job.Id}>", backgroundJobId);

            // Generate query
            var query = _queryProvider.Delete<BackgroundJobPropertyTable>().From(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable))
                                      .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)).And
                                                   .Column(c => c.Name).In.Parameters(properties))
                                      .Build(_compileOptions);
            _logger.Trace($"Deleting <{properties.Length}> properties for background job <{HiveLog.Job.Id}> using query <{query}>", backgroundJobId);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(backgroundJobId), backgroundJobId);
            properties.Execute(x => parameters.Add(x, x));

            var deleted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token));
            if (deleted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be deleted but only <{deleted}> were deleted");
            _logger.Log($"Deleting <{deleted}> properties for background job <{HiveLog.Job.Id}>", backgroundJobId);
        }

        /// <inheritdoc/>
        public virtual async Task<JobStorageData> GetBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Selecting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetBackgroundJobAsync)), x =>
            {
                return x.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable)).All()
                        .InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<StateTable>(c => c.BackgroundJobId))
                        .LeftJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(c => c.Id).EqualTo.Column<StatePropertyTable>(c => c.StateId))
                        .LeftJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id))
                        .OrderBy<StateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                        .OrderBy<StateTable>(c => c.ElectedDate, SortOrders.Ascending);
            });
            _logger.Trace($"Selecting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, connection.Environment);

            // Query and map
            MySqlBackgroundJobTable backgroundJob = null;
            List<BackgroundJobPropertyTable> properties = null;
            Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)> states = new Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)>();

            _ = await storageConnection.Connection.QueryAsync<MySqlBackgroundJobTable, StateTable, StatePropertyTable, BackgroundJobPropertyTable, Null>(new CommandDefinition(query, new { Id = id }, storageConnection.Transaction, cancellationToken: token), (b, s, sp, p) =>
            {
                // Job
                backgroundJob ??= b;

                // State
                if (!states.ContainsKey(s.Id)) states.Add(s.Id, (s, new List<StatePropertyTable>()));

                // State property
                if (sp != null && !states[sp.StateId].Properties.Select(x => x.Name).Contains(sp.Name, StringComparer.OrdinalIgnoreCase)) states[sp.StateId].Properties.Add(sp);

                // Property
                if (p != null && (properties == null || !properties.Select(x => x.Name).Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    properties ??= new List<BackgroundJobPropertyTable>();
                    properties.Add(p);
                }

                return Null.Value;
            }, $"{nameof(StateTable.Id)},{nameof(StatePropertyTable.StateId)},{nameof(Sql.Job.BackgroundJobPropertyTable.BackgroundJobId)}").ConfigureAwait(false);

            // Convert to storage format
            if (backgroundJob != null)
            {
                var job = backgroundJob.ToStorageFormat(_hiveOptions.Get(connection.Environment), _cache);
                job.Lock = backgroundJob.ToLockStorageFormat();
                job.States = states.Select(x =>
                {
                    var state = x.Value.State.ToStorageFormat();
                    if (x.Value.Properties.HasValue()) state.Properties = x.Value.Properties.Select(p => p.ToStorageFormat()).ToList();
                    return state;
                }).ToList();
                if (properties.HasValue()) job.Properties = properties.Select(x => x.ToStorageFormat()).ToList();

                _logger.Log($"Selected background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
                return job;
            }
            else
            {
                _logger.Warning($"Could not select background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
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
                var update = x.Update<BackgroundJobTable>().Table(BackgroundJobTable, typeof(BackgroundJobTable))
                              .Set.Column(c => c.LockedBy).To.Parameter(nameof(requester))
                              .Set.Column(c => c.LockedAt).To.CurrentDate(DateType.Utc)
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(requester))));

                var select = x.Select<BackgroundJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From(BackgroundJobTable, typeof(BackgroundJobTable))
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                return x.New().Append(update).Append(select);
            });
            _logger.Trace($"Trying to set lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}> using query <{query}>", id, connection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(requester), requester);
            var lockState = await storageConnection.Connection.QuerySingleAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Tried to set lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{requester}>", id, connection.Environment);
            return new LockStorageData()
            {
                LockedBy = lockState.LockedBy,
                LockedAtUtc = lockState.LockedAt,
                LockHeartbeatUtc = lockState.LockHeartbeat
            };
        }
        /// <inheritdoc/>
        public virtual async Task<LockStorageData> TryHeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Trying to set lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryHeartbeatLockAsync)), x =>
            {
                var update = x.Update<BackgroundJobTable>().Table(BackgroundJobTable, typeof(BackgroundJobTable))
                              .Set.Column(c => c.LockHeartbeat).To.CurrentDate(DateType.Utc)
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                          .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));

                var select = x.Select<BackgroundJobTable>()
                              .Column(c => c.LockedBy)
                              .Column(c => c.LockedAt)
                              .Column(c => c.LockHeartbeat)
                              .From(BackgroundJobTable, typeof(BackgroundJobTable))
                              .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)));

                return x.New().Append(update).Append(select);
            });
            _logger.Trace($"Trying to set lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(holder), holder);
            var lockState = await storageConnection.Connection.QuerySingleAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Tried to set lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
            return new LockStorageData()
            {
                LockedBy = lockState.LockedBy,
                LockedAtUtc = lockState.LockedAt,
                LockHeartbeatUtc = lockState.LockHeartbeat
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
                return x.Update<BackgroundJobTable>().Table(BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id)).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)));
            });
            _logger.Trace($"Trying remove lock from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", id, connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(holder), holder);
            var updated = await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

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
            var query = _queryProvider.Update<BackgroundJobTable>().Table(BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).In.Values(jobIds).And.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder)))
                        .Build(_compileOptions);
            _logger.Trace($"Trying to remove locks from <{ids.Length}> background jobs in environment <{HiveLog.Environment}> for <{HiveLog.Job.LockHolder}> using query <{query}>", connection.Environment, holder);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(holder), holder);
            var updated = await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

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
        public virtual async Task<(JobStorageData[] Results, long Total)> SearchBackgroundJobsAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Selecting the next max <{pageSize}> background jobs from page <{page}> in environment <{HiveLog.Environment}> matching the query conditions", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = _queryProvider.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable));
            countQuery.Where(x =>
            {
                (joinProperty, joinState, joinStateProperty) = BuildWhereStatement(x, parameters, queryConditions.Conditions);
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<StateTable>(x => x.BackgroundJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(x => x.Id).EqualTo.Column<StatePropertyTable>(x => x.StateId));

            // Select id of matching
            var selectIdQuery = countQuery.Clone().Column(x => x.Id).Limit(pageSize * (page - 1), pageSize);
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
            var selectQuery = _queryProvider.With().Cte("cte")
                                                        .As(selectIdQuery)
                                                   .Execute(_queryProvider.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable))
                                                                            .AllOf<MySqlBackgroundJobTable>()
                                                                            .AllOf<StateTable>()
                                                                            .AllOf<StatePropertyTable>()
                                                                            .AllOf<BackgroundJobPropertyTable>()
                                                                            .InnerJoin().Table("cte", 'c').On(x => x.Column(x => x.Id).EqualTo.Column('c', x => x.Id))
                                                                            .InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<StateTable>(c => c.BackgroundJobId))
                                                                            .LeftJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(c => c.Id).EqualTo.Column<StatePropertyTable>(c => c.StateId))
                                                                            .LeftJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                                                                            .OrderBy<StateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                                                                            .OrderBy<StateTable>(c => c.ElectedDate, SortOrders.Ascending));

            // Count total matching
            countQuery.CountAll();

            var query = _queryProvider.New().Append(countQuery).Append(selectQuery).Build(_compileOptions);
            _logger.Trace($"Selecting the next max <{pageSize}> background jobs from page <{page}> in environment <{HiveLog.Environment}> matching the query conditions using query <{query}>", storageConnection.Environment);

            //// Execute query
            var reader = await storageConnection.Connection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);
            var total = await reader.ReadSingleAsync<long>().ConfigureAwait(false);
            Dictionary<long, (MySqlBackgroundJobTable Job, Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)> States, List<BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (MySqlBackgroundJobTable Job, Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)> States, List<BackgroundJobPropertyTable> Properties)>();

            // Mapping
            _ = reader.Read<MySqlBackgroundJobTable, StateTable, StatePropertyTable, BackgroundJobPropertyTable, Null>((b, s, sp, p) =>
            {
                // Job
                Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)> states = null;
                List<BackgroundJobPropertyTable> properties = null;
                if (!backgroundJobs.TryGetValue(b.Id, out var backgroundJob))
                {
                    states = new Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)>();
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
                if (!states.ContainsKey(s.Id)) states.Add(s.Id, (s, new List<StatePropertyTable>()));

                // State property
                if (sp != null && !states[sp.StateId].Properties.Select(x => x.Name).Contains(sp.Name, StringComparer.OrdinalIgnoreCase)) states[sp.StateId].Properties.Add(sp);

                // Property
                if (p != null && (properties == null || !properties.Select(x => x.Name).Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    properties.Add(p);
                }

                return Null.Value;
            }, $"{nameof(StateTable.Id)},{nameof(StatePropertyTable.StateId)},{nameof(Sql.Job.BackgroundJobPropertyTable.BackgroundJobId)}");

            // Convert to storage format
            List<JobStorageData> jobStorageData = new List<JobStorageData>();
            foreach(var backgroundJob in backgroundJobs)
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

                _logger.Debug($"Selected background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> because it matched the query conditions", job.Id, connection.Environment);
                jobStorageData.Add(job);
            }

            _logger.Log($"Selected <{jobStorageData.Count}> background jobs from page <{page}> in environment <{HiveLog.Environment}> out of the total <{total}> matching the query conditions", storageConnection.Environment);
            return (jobStorageData.ToArray(), total);
        }
        /// <inheritdoc/>
        public virtual async Task<long> CountBackgroundJobsAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, CancellationToken token = default)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Counting the amount of background jobs in environment <{HiveLog.Environment}> matching the query conditions", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = _queryProvider.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable));
            countQuery.Where(x =>
            {
                (joinProperty, joinState, joinStateProperty) = BuildWhereStatement(x, parameters, queryConditions.Conditions);
                return x.LastBuilder;
            });

            // Join if needed
            if (joinProperty) countQuery.InnerJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<StateTable>(x => x.BackgroundJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(x => x.Id).EqualTo.Column<StatePropertyTable>(x => x.StateId));

            // Count total matching
            countQuery.CountAll();

            var query = countQuery.Build(_compileOptions);
            _logger.Trace($"Counting the amount of background jobs in environment <{HiveLog.Environment}> matching the query conditions using query <{query}>", storageConnection.Environment);

            //// Execute query
            var total = await storageConnection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);
            _logger.Log($"Counted <{total}> background jobs in environment <{HiveLog.Environment}> matching the query conditions", storageConnection.Environment);
            return total;
        }
        /// <inheritdoc/>
        public virtual async Task<(JobStorageData[] Results, long Total)> LockBackgroundJobsAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {            
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Trying to lock the next <{limit}> background jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query conditions", storageConnection.Environment);

            //// Generate query
            var parameters = new DynamicParameters();
            // Used to get the updated rows
            var executionId = Guid.NewGuid().ToString();
            parameters.Add(nameof(executionId), executionId);
            parameters.Add(nameof(requester), requester);

            bool joinProperty = false;
            bool joinState = false;
            bool joinStateProperty = false;

            var countQuery = _queryProvider.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable));
            countQuery.Where(x =>
            {
                var builder = x.WhereGroup(x =>
                {
                    (joinProperty, joinState, joinStateProperty) = BuildWhereStatement(x, parameters, queryConditions.Conditions);
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
            if (joinProperty) countQuery.InnerJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<BackgroundJobPropertyTable>(x => x.BackgroundJobId));
            if (joinState) countQuery.InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(x => x.Id).EqualTo.Column<StateTable>(x => x.BackgroundJobId));
            if (joinStateProperty) countQuery.InnerJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(x => x.Id).EqualTo.Column<StatePropertyTable>(x => x.StateId));

            // Select the ids to update because MariaDB update refuses to use the same index as selects and it rather wants to scan the whole table
            var selectIdQuery = countQuery.Clone().Column(x => x.Id).ForUpdate().Limit(100);          
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

            // Count total matching
            countQuery.CountAll();

            // Determine what to update and keep an update lock
            var query = _queryProvider.New().Append(countQuery).Append(selectIdQuery).Build(_compileOptions);
            _logger.Trace($"Selecting the ids of the next <{limit}> background jobs in environment <{HiveLog.Environment}> to lock for <{requester}> matching the query conditions using query <{query}>", storageConnection.Environment);

            var reader = await storageConnection.Connection.QueryMultipleAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);
            var total = await reader.ReadSingleAsync<long>().ConfigureAwait(false);
            var ids = await reader.ReadAsync<long>().ConfigureAwait(false);

            if (!ids.HasValue())
            {
                _logger.Log($"Locked no background jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query conditions", storageConnection.Environment);
                return (Array.Empty<JobStorageData>(), total);
            }

            // Update matching jobs
            var updateQuery = _queryProvider.Update<MySqlBackgroundJobTable>().Table(BackgroundJobTable, typeof(MySqlBackgroundJobTable))
                                            .Set.Column(x => x.LockedBy).To.Parameter(nameof(requester))
                                            .Set.Column(x => x.ExecutionId).To.Parameter(nameof(executionId))
                                            .Set.Column(x => x.LockHeartbeat).To.CurrentDate(DateType.Utc)
                                            .Set.Column(x => x.LockedAt).To.CurrentDate(DateType.Utc)
                                            .Where(x => x.Column(x => x.Id).In.Values(ids));

            // Select updated background jobs
            var selectQuery = _queryProvider.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable))
                                            .AllOf<MySqlBackgroundJobTable>()
                                            .AllOf<StateTable>()
                                            .AllOf<StatePropertyTable>()
                                            .AllOf<BackgroundJobPropertyTable>()
                                            .InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<StateTable>(c => c.BackgroundJobId))
                                            .LeftJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(c => c.Id).EqualTo.Column<StatePropertyTable>(c => c.StateId))
                                            .LeftJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                                            .Where(x => x.Column(x => x.Id).In.Values(ids))
                                            .OrderBy<StateTable>(c => c.BackgroundJobId, SortOrders.Ascending)
                                            .OrderBy<StateTable>(c => c.ElectedDate, SortOrders.Ascending);

            query = _queryProvider.New().Append(updateQuery).Append(selectQuery).Build(_compileOptions);
            _logger.Trace($"Trying to lock the next <{limit}> background jobs in environment <{HiveLog.Environment}> for <{requester}> matching the query conditions using query <{query}>", storageConnection.Environment);

            // Execute query
            Dictionary<long, (MySqlBackgroundJobTable Job, Dictionary<long, (StateTable State, Dictionary<string, StatePropertyTable> Properties)> States, Dictionary<string, BackgroundJobPropertyTable> Properties)> backgroundJobs = new Dictionary<long, (MySqlBackgroundJobTable Job, Dictionary<long, (StateTable State, Dictionary<string, StatePropertyTable> Properties)> States, Dictionary<string, BackgroundJobPropertyTable> Properties)>();
            _ = await storageConnection.Connection.QueryAsync<MySqlBackgroundJobTable, StateTable, StatePropertyTable, BackgroundJobPropertyTable, Null>(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token), (b, s, sp, p) =>
            {
                // Job
                Dictionary<long, (StateTable State, Dictionary<string, StatePropertyTable> Properties)> states = null;
                Dictionary<string, BackgroundJobPropertyTable> properties = null;
                if (!backgroundJobs.TryGetValue(b.Id, out var backgroundJob))
                {
                    states = new Dictionary<long, (StateTable State, Dictionary<string, StatePropertyTable> Properties)>();
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
                if (!states.ContainsKey(s.Id)) states.Add(s.Id, (s, new Dictionary<string, StatePropertyTable>(StringComparer.OrdinalIgnoreCase)));

                // State property
                if (sp != null && !states[sp.StateId].Properties.ContainsKey(sp.Name)) states[sp.StateId].Properties.Add(sp.Name, sp);

                // Property
                if (p != null && !properties.ContainsKey(p.Name)) properties.Add(p.Name, p);

                return Null.Value;
            }, $"{nameof(StateTable.Id)},{nameof(StatePropertyTable.StateId)},{nameof(Sql.Job.BackgroundJobPropertyTable.BackgroundJobId)}").ConfigureAwait(false);

            // Convert to storage format
            List<JobStorageData> jobStorageData = new List<JobStorageData>();

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

                _logger.Debug($"Selected background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> with lock for <{requester}> because it matched the query conditions", job.Id, connection.Environment);
                jobStorageData.Add(job);
            }

            _logger.Log($"Locked <{jobStorageData.Count}> background jobs in environment <{HiveLog.Environment}> out of the total <{total}> for <{HiveLog.Job.LockHolder}> matching the query conditions", storageConnection.Environment, requester);
            return (jobStorageData.ToArray(), total);
        }
                
        private (bool requiresProperty, bool requiresState, bool requiresStateProperty) BuildWhereStatement(IStatementConditionExpressionBuilder<MySqlBackgroundJobTable> builder, DynamicParameters parameters, IEnumerable<BackgroundJobConditionGroupExpression> queryConditions)
        {
            builder.ValidateArgument(nameof(builder));
            queryConditions.ValidateArgument(nameof(queryConditions));

            //// Try and determine if we can just build a query using joins on some tables
            // We can only join if they are all OR statements (exception for the last)
            var propertyConditions = GetConditions(queryConditions).Where(x => x.Condition.Target == QueryBackgroundJobConditionTarget.Property).ToArray();
            bool canJoinProperty = propertyConditions.Take(propertyConditions.Length-1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);

            // We can only join on state when they are all OR statements (exception for the last) unless they both target current and past states
            var stateConditions = GetConditions(queryConditions).Where(x => (x.Condition.CurrentStateComparison != null && x.Condition.CurrentStateComparison.Target != QueryBackgroundJobStateConditionTarget.Property) || (x.Condition.PastStateComparison != null && x.Condition.PastStateComparison.Target != QueryBackgroundJobStateConditionTarget.Property)).ToArray();
            bool onAnyState = stateConditions.Count(x => x.Condition.CurrentStateComparison != null) > 0 && stateConditions.Count(x => x.Condition.PastStateComparison != null) > 0;
            bool canJoinState = !onAnyState && stateConditions.Take(stateConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);

            // We can only join on state property when they are all OR statements (exception for the last) unless they both target current and past states
            bool canJoinStateProperty = false;
            if (canJoinState)
            {
                var statePropertyConditions = GetConditions(queryConditions).Where(x => (x.Condition.CurrentStateComparison != null && x.Condition.CurrentStateComparison.Target == QueryBackgroundJobStateConditionTarget.Property) || (x.Condition.PastStateComparison != null && x.Condition.PastStateComparison.Target == QueryBackgroundJobStateConditionTarget.Property)).ToArray();
                canJoinStateProperty = statePropertyConditions.Take(statePropertyConditions.Length - 1).All(x => x.Operator == null || x.Operator == QueryLogicalOperator.Or);
            }

            var (requiresProperty, requiresState, requiresStateProperty) = BuildWhereStatement(builder, parameters, queryConditions, canJoinProperty, canJoinState, canJoinStateProperty);
            return (requiresProperty && canJoinProperty, requiresState && canJoinState, requiresStateProperty && canJoinStateProperty);
        }
        private (bool requiresProperty, bool requiresState, bool requiresStateProperty) BuildWhereStatement(IStatementConditionExpressionBuilder<MySqlBackgroundJobTable> builder, DynamicParameters parameters, IEnumerable<BackgroundJobConditionGroupExpression> queryConditions, bool canJoinProperty, bool canJoinState, bool canJoinStateProperty)
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
                            var (conditionRequiresProperty, conditionRequiresState, conditionRequiresStateProperty) = BuildWhereStatement(x, parameters, expression.Group.Conditions, canJoinProperty, canJoinState, canJoinStateProperty);

                            if (conditionRequiresProperty) requiresProperty = true;
                            if (conditionRequiresState) requiresState = true;
                            if (conditionRequiresStateProperty) requiresStateProperty = true;

                            return x.LastBuilder;
                        });
                    }
                    else
                    {
                        var (conditionRequiresProperty, conditionRequiresState, conditionRequiresStateProperty) = AddCondition(builder, expression.Condition, parameters, canJoinProperty, canJoinState, canJoinStateProperty);

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
                        var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + i + 1}";
                        parameters.Add(parameter, x);
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
                    parameters.Add(patternParameter, pattern);
                    return;
                default: throw new NotSupportedException($"Comparator <{comparison.Comparator}> is not supported");
            }

            var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
            expressionBuilder.Parameter(parameter);
            parameters.Add(parameter, comparison.Value);
        }
        private void AddComparison<TBuilder, TTable>(IStatementConditionExpressionBuilder<TBuilder> builder, BackgroundJobPropertyCondition condition, DynamicParameters parameters)
            where TTable : BasePropertyTable
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            var parameter = $"@Parameter{parameters.ParameterNames.GetCount() + 1}";
            parameters.Add(parameter, condition.Name);

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
                    default: throw new NotSupportedException($"Storage type <{condition.Type}> is not supported");
                }

                return x.LastBuilder;
            });
        }
        private (bool requiresProperty, bool requiresState, bool requiresStateProperty) AddCondition(IStatementConditionExpressionBuilder<MySqlBackgroundJobTable> builder, BackgroundJobCondition condition, DynamicParameters parameters, bool canJoinProperty, bool canJoinState, bool canJoinStateProperty)
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresProperty = false;
            bool requiresState = false;
            bool requiresStateProperty = false;

            switch (condition.Target)
            {
                case QueryBackgroundJobConditionTarget.Queue:
                    AddComparison(builder, x => x.Column(x => x.Queue), condition.QueueComparison, parameters);
                    break;
                case QueryBackgroundJobConditionTarget.LockedBy:
                    AddComparison(builder, x => x.Column(x => x.LockedBy), condition.LockedByComparison, parameters);
                    break;
                case QueryBackgroundJobConditionTarget.Priority:
                    AddComparison(builder, x => x.Column(x => x.Priority), condition.PriorityComparison, parameters);
                    break;
                case QueryBackgroundJobConditionTarget.CreatedAt:
                    AddComparison(builder, x => x.Column(x => x.CreatedAt), condition.CreatedAtComparison, parameters);
                    break;
                case QueryBackgroundJobConditionTarget.ModifiedAt:
                    AddComparison(builder, x => x.Column(x => x.ModifiedAt), condition.ModifiedAtComparison, parameters);
                    break;
                case QueryBackgroundJobConditionTarget.Property:
                    // Exists in because we can join property table
                    if (!canJoinProperty)
                    {
                        var propertyBuilder = _queryProvider.Select<BackgroundJobPropertyTable>().Value(1).From(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).Where(x =>
                        {
                            var b = x.Column(x => x.BackgroundJobId).EqualTo.Column<MySqlBackgroundJobTable>(x => x.Id).And;
                            AddComparison<BackgroundJobPropertyTable, BackgroundJobPropertyTable>(b, condition.PropertyComparison, parameters);
                            return b.LastBuilder;
                        });
                        builder.ExistsIn(propertyBuilder);
                    }
                    else
                    {
                        requiresProperty = true;
                        AddComparison<MySqlBackgroundJobTable, BackgroundJobPropertyTable>(builder, condition.PropertyComparison, parameters);
                    }

                    break;
                case QueryBackgroundJobConditionTarget.CurrentState:
                    requiresState = true;
                    requiresStateProperty = AddCondition(builder, condition.CurrentStateComparison, parameters, true, canJoinState, canJoinStateProperty);                    
                    break;
                case QueryBackgroundJobConditionTarget.PastState:
                    requiresState = true;
                    requiresStateProperty = AddCondition(builder, condition.CurrentStateComparison, parameters, false, canJoinState, canJoinStateProperty);
                    break;
                default: throw new NotSupportedException($"Condition target <{condition.Target}> is not known");
            }

            return (requiresProperty, requiresState, requiresStateProperty);
        }

        private bool AddCondition<T>(IStatementConditionExpressionBuilder<T> builder, BackgroundJobStateCondition condition, DynamicParameters parameters, bool isCurrentState, bool canJoinState, bool canJoinStateProperty)
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            bool requiresProperty = false;

            if (canJoinState)
            {
                builder.WhereGroup(x =>
                {
                    _ = x.Column<StateTable>(c => c.IsCurrent).EqualTo.Value(isCurrentState).And;

                    if (condition.Target == QueryBackgroundJobStateConditionTarget.Property)
                    {
                        if (!canJoinStateProperty)
                        {
                            var subBuilder = _queryProvider.Select<StatePropertyTable>().Value(1).From(BackgroundJobStatePropertyTable, typeof(StatePropertyTable))
                                                            .Where(x =>
                                                            {
                                                                var b = x.Column(x => x.StateId).EqualTo.Column<StateTable>(x => x.Id).And;
                                                                AddComparison(b, condition, parameters);
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
                    AddComparison(x, condition, parameters);
                    return x.LastBuilder;
                });            
            }
            else
            {
                var subBuilder = _queryProvider.Select<StateTable>().Value(1).From(BackgroundJobStateTable, typeof(StateTable))
                                .Where(x =>
                                {
                                    var b = x.Column(x => x.BackgroundJobId).EqualTo.Column<MySqlBackgroundJobTable>(x => x.Id).And.Column(x => x.IsCurrent).EqualTo.Value(isCurrentState).And;
                                    AddComparison(b, condition, parameters);
                                    return b.LastBuilder;
                                });
                if (condition.Target == QueryBackgroundJobStateConditionTarget.Property) subBuilder.InnerJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column(x => x.Id).EqualTo.Column<StatePropertyTable>(x => x.StateId));
                builder.ExistsIn(subBuilder);
            }

            return requiresProperty;
        }

        private void AddComparison<T>(IStatementConditionExpressionBuilder<T> builder, BackgroundJobStateCondition condition, DynamicParameters parameters)
        {
            builder.ValidateArgument(nameof(builder));
            condition.ValidateArgument(nameof(condition));
            parameters.ValidateArgument(nameof(parameters));

            switch (condition.Target)
            {
                case QueryBackgroundJobStateConditionTarget.Name:
                    AddComparison(builder, x => x.Column<StateTable>(x => x.Name), condition.NameComparison, parameters);
                    break;
                case QueryBackgroundJobStateConditionTarget.Reason:
                    AddComparison(builder, x => x.Column<StateTable>(x => x.Reason), condition.ReasonComparison, parameters);
                    break;
                case QueryBackgroundJobStateConditionTarget.ElectedDate:
                    AddComparison(builder, x => x.Column<StateTable>(x => x.Reason), condition.ElectedDateComparison, parameters);
                    break;
                case QueryBackgroundJobStateConditionTarget.Property:
                    AddComparison<T, StatePropertyTable>(builder, condition.PropertyComparison, parameters);
                    break;
                default: throw new NotSupportedException($"Target <{condition.Target}> is not supported");
            }
        }

        private IEnumerable<(BackgroundJobCondition Condition, QueryLogicalOperator? Operator)> GetConditions(IEnumerable<BackgroundJobConditionGroupExpression> expressions)
        {
            foreach (var expression in expressions)
            {
                if (expression.Expression.IsGroup)
                {
                    foreach(var subCondition in GetConditions(expression.Expression.Group.Conditions))
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
        
        /// <inheritdoc/>
        public virtual async Task CreateBackgroundJobLogsAsync(IStorageConnection connection, string id, IEnumerable<LogEntry> logEntries, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            logEntries.ValidateArgumentNotNullOrEmpty(nameof(logEntries));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            var count = logEntries.GetCount();
            _logger.Log($"Inserting <{count}> log entries for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(CreateBackgroundJobLogsAsync)}.{count}"), x =>
            {
                var insertQuery = x.Insert<LogEntry>().Into(table: BackgroundJobLogTable).ColumnsOf(nameof(LogEntry.CreatedAt)).Column(BackgroundJobForeignKeyColumn);
                logEntries.Execute((i, x) => {
                    insertQuery.Values(x => x.Parameter(p => p.LogLevel, i)
                                      ,x => x.Parameter(p => p.Message, i)
                                      ,x => x.Parameter(p => p.ExceptionType, i)
                                      ,x => x.Parameter(p => p.ExceptionMessage, i)
                                      ,x => x.Parameter(p => p.ExceptionStackTrace, i)
                                      ,x => x.Parameter(p => p.CreatedAtUtc, i)
                                      ,x => x.Parameter(nameof(id)));
                });
                return insertQuery;
            });
            _logger.Trace($"Inserting <{count}> log entries for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            logEntries.Execute((i, x) =>
            {
                parameters.Add($"{nameof(x.LogLevel)}{i}", x.LogLevel);
                parameters.Add($"{nameof(x.Message)}{i}", x.Message);
                parameters.Add($"{nameof(x.ExceptionType)}{i}", x.ExceptionType);
                parameters.Add($"{nameof(x.ExceptionMessage)}{i}", x.ExceptionMessage);
                parameters.Add($"{nameof(x.ExceptionStackTrace)}{i}", x.ExceptionStackTrace);
                parameters.Add($"{nameof(x.CreatedAtUtc)}{i}", x.CreatedAtUtc);
            });

            var inserted = await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);
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
                var getQuery = x.Select<LogEntry>().ColumnsOf(nameof(LogEntry.CreatedAt))
                                .From(BackgroundJobLogTable, typeof(LogEntry))
                                .Limit(SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(page)), SQL.QueryBuilder.Sql.Expressions.Parameter(nameof(pageSize)))
                                .Where(x => x.Column(BackgroundJobForeignKeyColumn).EqualTo.Parameter(nameof(id)))
                                .OrderBy(x => x.CreatedAtUtc, mostRecentFirst ? SortOrders.Descending : SortOrders.Ascending);

                if (logLevels.HasValue()) getQuery.Where(x => x.Column(x => x.LogLevel).In.Parameters(logLevels.Select((i, x) => $"{nameof(logLevels)}{i}")));
                return getQuery;
            });
            _logger.Trace($"Fetching up to <{pageSize}> logs from page <{page}> of background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(page), (page - 1) * pageSize);
            parameters.Add(nameof(pageSize), pageSize);
            if (logLevels.HasValue()) logLevels.Execute((i, x) => parameters.Add($"{nameof(logLevels)}{i}", x));

            var logs = (await storageConnection.Connection.QueryAsync<LogEntry>(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

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
                return x.Select().Column(DataValueColumn)
                        .From(BackgroundJobDataTable)
                        .Where(x => x.Column(BackgroundJobForeignKeyColumn).EqualTo.Parameter(nameof(id))
                                 .And.Column(DataNameColumn).EqualTo.Parameter(nameof(name)));
            });
            _logger.Trace($"Trying to get data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(name), name);

            var value = await storageConnection.Connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Fetched data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>: {value ?? "NULL"}", id, storageConnection.Environment);
            return (value != null, value);
        }
        /// <inheritdoc/>
        public async Task SetBackgroundJobDataAsync(IStorageConnection connection, string id, string name, string value, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            value.ValidateArgument(nameof(value));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Saving data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(SetBackgroundJobDataAsync)), b =>
            {
                var updateQuery = b.Update().Table(BackgroundJobDataTable)
                                   .Set.Column(DataValueColumn).To.Parameter(nameof(value))
                                   .Where(x => x.Column(BackgroundJobForeignKeyColumn).EqualTo.Parameter(nameof(id))
                                            .And.Column(DataNameColumn).EqualTo.Parameter(nameof(name)));

                // Insert if update did not update anything
                var insertQuery = b.If().Condition(x => x.RowCount().EqualTo.Value(0))
                                        .Then(x => x.Append(b.Insert().Into(table: BackgroundJobDataTable).Columns(BackgroundJobForeignKeyColumn, DataNameColumn, DataValueColumn)
                                                             .Parameters(nameof(id), nameof(name), nameof(value))
                                                           )
                                        );

                return b.New().Append(updateQuery).Append(insertQuery);
            });
            _logger.Trace($"Saving data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> using query <{query}>", id, storageConnection.Environment);

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(name), name);
            parameters.Add(nameof(value), value);
            await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, storageConnection.Environment);
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

            return $"{_hiveOptions.Value.CachePrefix}.{nameof(HiveMindMySqlStorage)}.{key}";
        }

        
    }
}
