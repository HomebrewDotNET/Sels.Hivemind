using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Threading;
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

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Persists HiveMind state in a MySql database.
    /// </summary>
    public class HiveMindMySqlStorage : IStorage
    {
        // Fields
        private readonly IOptionsSnapshot<HiveMindOptions> _hiveOptions;
        private readonly IMemoryCache? _cache;
        private readonly HiveMindMySqlStorageOptions _options;
        private readonly ICachedSqlQueryProvider _queryProvider;
        private readonly string _environment;
        private readonly string _connectionString;
        private readonly ILogger? _logger;

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

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="hiveMindOptions">The global hive mind options for this instance</param>
        /// <param name="cache">Optional cache used for type conversion</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, IMemoryCache? cache, HiveMindMySqlStorageOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger? logger = null)
        {
            _hiveOptions = hiveMindOptions.ValidateArgument(nameof(hiveMindOptions));
            _cache = cache;
            _options = options.ValidateArgument(nameof(options));
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            _connectionString = connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            _queryProvider = queryProvider.ValidateArgument(nameof(queryProvider)).CreateSubCachedProvider(x => x.WithExpressionCompileOptions(_compileOptions));
            _logger = logger;
        }

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="options">The options for this instance</param>
        /// <param name="cache">Optional cache used for type conversion</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, IMemoryCache? cache, HiveMindMySqlStorageOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger<HiveMindMySqlStorage>? logger = null) : this(hiveMindOptions, cache, options, environment, connectionString, queryProvider, logger.CastToOrDefault<ILogger>())
        {
        }

        /// <inheritdoc/>
        public virtual async Task<IStorageConnection> OpenConnectionAsync(bool startTransaction, CancellationToken token = default)
        {
            _logger.Log($"Opening new connection to MySql storage in environment <{_environment}>");

            var connection = new MySqlConnection(_connectionString);
            MySqlStorageConnection? storageConnection = null;
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

            _logger.Log($"Inserting new background job in environment <{_environment}>");
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
            _logger.Trace($"Inserting new background job in environment <{_environment}> using query <{query}>");

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
            _logger.Log($"Inserted background job <{id}> in environment <{_environment}>");
            return id.ToString();
        }
        /// <inheritdoc/>
        public virtual async Task<bool> UpdateBackgroundJobAsync(JobStorageData jobData, IStorageConnection connection, bool releaseLock, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Updating background job <{jobData.Id}> in environment <{_environment}>");
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
            _logger.Trace($"Updating background job <{jobData.Id}> in environment <{_environment}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(jobData.Id), jobData.Id);
            parameters.Add(nameof(jobData.Queue), jobData.Queue);
            parameters.Add(nameof(jobData.Priority), jobData.Priority);
            parameters.Add(nameof(jobData.ExecutionId), jobData.ExecutionId);
            parameters.Add(nameof(jobData.ModifiedAtUtc), jobData.ModifiedAtUtc);
            parameters.Add(nameof(jobData.Lock.LockedBy), releaseLock ? jobData.Lock.LockedBy : null);
            parameters.Add(nameof(jobData.Lock.LockedAtUtc), releaseLock ? jobData.Lock.LockedAtUtc : (DateTime?)null);
            parameters.Add(nameof(jobData.Lock.LockHeartbeatUtc), releaseLock ? jobData.Lock.LockHeartbeatUtc : (DateTime?)null);
            parameters.Add(nameof(releaseLock), releaseLock);
            parameters.Add(nameof(holder), holder);

            var updated = await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            if(updated != 1)
            {
                _logger.Warning($"Could not update background job <{jobData.Id}> in environment <{_environment}>");
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

            _logger.Log($"Inserting <{states.Length}> new states for background job <{backgroundJobId}>");

            // Reset is current on existing states
            var resetQuery = _queryProvider.Update<StateTable>().Table(BackgroundJobStateTable, typeof(StateTable))
                                           .Set.Column(c => c.IsCurrent).To.Value(false)
                                           .Where(w => w.Column(c => c.BackgroundJobId).EqualTo.Value(backgroundJobId))
                                           .Build(_compileOptions);
            _logger.Trace($"Resetting {nameof(StateTable.IsCurrent)} to false for existing states using query <{resetQuery}>");
            await connection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(resetQuery, null, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            // Insert new
            states.Last().State.IsCurrent = true;
            foreach (var (state, properties) in states)
            {
                state.BackgroundJobId = backgroundJobId;
                state.CreatedAt = DateTime.UtcNow;
                state.ModifiedAt = DateTime.UtcNow;

                // Insert state
                _logger.Debug($"Inserting state <{state.Name}> for background job <{backgroundJobId}>");
                var parameters = new DynamicParameters();
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
                _logger.Trace($"Inserting state <{state.Name}> for background job <{backgroundJobId}> using query <{query}>");

                var stateId = await connection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);
                _logger.Debug($"Inserted state <{state.Name}> for background job <{backgroundJobId}> with id <{stateId}>");

                // Insert properties
                if (properties.HasValue())
                {
                    properties.Execute(x => x.StateId = stateId);
                    await InsertStatePropertiesAsync(connection, properties, token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Inserted <{states.Length}> new states for background job <{backgroundJobId}>");
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

            _logger.Log($"Inserting <{properties.Length}> new properties for background job <{backgroundJobId}>");
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
            _logger.Trace($"Inserting <{properties.Length}> properties for background job <{backgroundJobId}> using query <{query}>");

            var inserted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);
            if (inserted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be inserted but only <{inserted}> were inserted");
            _logger.Log($"Inserted <{inserted}> new properties for background job <{backgroundJobId}>");
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

            _logger.Log($"Updating <{properties.Length}> properties for background job <{backgroundJobId}>");
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
            _logger.Trace($"Updating each property for background job <{backgroundJobId}> using query <{query}>");

            foreach(var property in properties)
            {
                _logger.Debug($"Updating property <{property.Name}> for background job <{backgroundJobId}>");
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
                _logger.Debug($"Updated property <{property.Name}> for background job <{backgroundJobId}>");
            }

            _logger.Log($"Updated <{properties.Length}> properties for background job <{backgroundJobId}>");
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

            _logger.Log($"Deleting <{properties.Length}> properties for background job <{backgroundJobId}>");

            // Generate query
            var query = _queryProvider.Delete<BackgroundJobPropertyTable>().From(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable))
                                      .Where(x => x.Column(c => c.BackgroundJobId).EqualTo.Parameter(nameof(backgroundJobId)).And
                                                   .Column(c => c.Name).In.Parameters(properties))
                                      .Build(_compileOptions);
            _logger.Trace($"Deleting <{properties.Length}> properties for background job <{backgroundJobId}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(backgroundJobId), backgroundJobId);
            properties.Execute(x => parameters.Add(x, x));

            var deleted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token));
            if (deleted != properties.Length) throw new InvalidOperationException($"Expected <{properties.Length}> properties to be deleted but only <{deleted}> were deleted");
            _logger.Log($"Deleting <{deleted}> properties for background job <{backgroundJobId}>");
        }

        /// <inheritdoc/>
        public virtual async Task<JobStorageData?> GetBackgroundJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            var storageConnection = GetStorageConnection(connection);

            // Generate query
            _logger.Log($"Selecting background job <{id}> in environment <{connection.Environment}>");
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(GetBackgroundJobAsync)), x =>
            {
                return x.Select<MySqlBackgroundJobTable>().From(BackgroundJobTable, typeof(MySqlBackgroundJobTable)).All()
                        .InnerJoin().Table(BackgroundJobStateTable, typeof(StateTable)).On(x => x.Column(c => c.Id).EqualTo.Column<StateTable>(c => c.BackgroundJobId))
                        .LeftJoin().Table(BackgroundJobStatePropertyTable, typeof(StatePropertyTable)).On(x => x.Column<StateTable>(c => c.Id).EqualTo.Column<StatePropertyTable>(c => c.StateId))
                        .LeftJoin().Table(BackgroundJobPropertyTable, typeof(BackgroundJobPropertyTable)).On(x => x.Column(c => c.Id).EqualTo.Column<BackgroundJobPropertyTable>(c => c.BackgroundJobId))
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(c => c.Id))
                        .OrderBy<StateTable>(c => c.ElectedDate, SortOrders.Ascending);
            });
            _logger.Trace($"Selecting background job <{id}> in environment <{connection.Environment}> using query <{query}>");

            // Query and map
            MySqlBackgroundJobTable? backgroundJob = null;
            List<BackgroundJobPropertyTable>? properties = null;
            Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)> states = new Dictionary<long, (StateTable State, List<StatePropertyTable> Properties)>();

            _ = await storageConnection.Connection.QueryAsync<MySqlBackgroundJobTable, StateTable, StatePropertyTable, BackgroundJobPropertyTable, Null>(new CommandDefinition(query, new { Id = id }, storageConnection.Transaction, cancellationToken: token), (b, s, sp, p) =>
            {
                // Job
                backgroundJob ??= b;

                // State
                if (!states.ContainsKey(s.Id)) states.Add(s.Id, (s, new List<StatePropertyTable>()));

                // State property
                if(sp != null && !states[sp.StateId].Properties.Select(x => x.Name).Contains(sp.Name, StringComparer.OrdinalIgnoreCase)) states[sp.StateId].Properties.Add(sp);

                // Property
                if(p != null && (properties == null || !properties.Select(x => x.Name).Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    properties ??= new List<BackgroundJobPropertyTable>();
                    properties.Add(p);
                }

                return Null.Value;
            }, $"{nameof(StateTable.Id)},{nameof(StatePropertyTable.StateId)},{nameof(Sql.Job.BackgroundJobPropertyTable.BackgroundJobId)}").ConfigureAwait(false);

            // Convert to storage format
            if(backgroundJob != null)
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

                _logger.Log($"Selected background job <{id}> in environment <{connection.Environment}>");
                return job;
            }
            else
            {
                _logger.Warning($"Could not select background job <{id}> in environment <{connection.Environment}>");
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
            _logger.Log($"Trying to set lock on background job <{id}> in environment <{connection.Environment}> for <{requester}>");
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
            _logger.Trace($"Trying to set lock on background job <{id}> in environment <{connection.Environment}> for <{requester}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(requester), requester);
            var lockState = await storageConnection.Connection.QuerySingleAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Tried to set lock on background job <{id}> in environment <{connection.Environment}> for <{requester}>");
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
            _logger.Log($"Trying to set lock heartbeat on background job <{id}> in environment <{connection.Environment}> for <{holder}>");
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
            _logger.Trace($"Trying to set lock heartbeat on background job <{id}> in environment <{connection.Environment}> for <{holder}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(holder), holder);
            var lockState = await storageConnection.Connection.QuerySingleAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Tried to set lock heartbeat on background job <{id}> in environment <{connection.Environment}> for <{holder}>");
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
            _logger.Log($"Trying to remove lock from background job <{id}> in environment <{connection.Environment}> for <{holder}>");
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(UnlockBackgroundJobAsync)), x =>
            {
                return x.Update<BackgroundJobTable>().Table(BackgroundJobTable, typeof(BackgroundJobTable))
                        .Set.Column(c => c.LockedBy).To.Null()
                        .Set.Column(c => c.LockedAt).To.Null()
                        .Set.Column(c => c.LockHeartbeat).To.Null()
                        .Where(x => x.Column(c => c.Id).EqualTo.Parameter(nameof(id))
                                    .And.WhereGroup(x => x.Column(c => c.LockedBy).IsNull.Or.Column(c => c.LockedBy).EqualTo.Parameter(nameof(holder))));
            });
            _logger.Trace($"Trying remove lock from background job <{id}> in environment <{connection.Environment}> for <{holder}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(holder), holder);
            var updated = await storageConnection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, storageConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            if(updated > 0)
            {
                _logger.Log($"Removed lock from background job <{id}> in environment <{connection.Environment}> for <{holder}>");
                return true;
            }
            else
            {
                _logger.Warning($"Could not remove lock from background job <{id}> in environment <{connection.Environment}> for <{holder}>");
                return false;
            }
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

    /// <inheritdoc cref="IStorageConnection"/>
    public class MySqlStorageConnection : IStorageConnection
    {
        // Fields
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly List<Delegates.Async.AsyncAction<CancellationToken>> _commitActions = new List<Delegates.Async.AsyncAction<CancellationToken>>();
        private readonly List<Delegates.Async.AsyncAction<CancellationToken>> _committedActions = new List<Delegates.Async.AsyncAction<CancellationToken>>();

        // Properties
        /// <inheritdoc/>
        public IStorage Storage { get; }
        /// <inheritdoc/>
        public string Environment { get; }
        /// <inheritdoc/>
        public bool HasTransaction => Transaction != null;
        /// <summary>
        /// The opened connection to the database.
        /// </summary>
        public MySqlConnection Connection { get; private set; }
        /// <summary>
        /// The transaction if one is opened.
        /// </summary>
        public MySqlTransaction? Transaction { get; private set; }

        public MySqlStorageConnection(MySqlConnection connection, IStorage storage, string environment)
        {
            Connection = connection.ValidateArgument(nameof(connection));
            Storage = storage.ValidateArgument(nameof(storage));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
        /// <inheritdoc/>
        public async Task BeginTransactionAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                if (Transaction == null)
                {
                    Transaction = await Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted ,token).ConfigureAwait(false);
                }
            }
        }
        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                Delegates.Async.AsyncAction<CancellationToken>[]? preActions = null;
                Delegates.Async.AsyncAction<CancellationToken>[]? postActions = null;
                lock (_commitActions)
                {
                    preActions = _commitActions.ToArray();
                    _commitActions.Clear();
                }
                lock (_committedActions)
                {
                    postActions = _committedActions.ToArray();
                    _committedActions.Clear();
                }

                foreach (var action in preActions)
                {
                    await action(token).ConfigureAwait(false);
                }

                if (Transaction != null) await Transaction.CommitAsync(token).ConfigureAwait(false);
                Transaction = null;

                foreach (var action in postActions)
                {
                    await action(token).ConfigureAwait(false);
                }
            }
        }
        /// <inheritdoc/>
        public void OnCommitting(Delegates.Async.AsyncAction<CancellationToken> action)
        {
            action.ValidateArgument(nameof(action));

            lock (_commitActions)
            {
                _commitActions.Add(action);
            }
        }
        /// <inheritdoc/>
        public void OnCommitted(Delegates.Async.AsyncAction<CancellationToken> action)
        {
            action.ValidateArgument(nameof(action));

            lock (_committedActions)
            {
                _committedActions.Add(action);
            }
        }
        public async ValueTask DisposeAsync()
        {
            await using (await _lock.LockAsync().ConfigureAwait(false))
            {
                var exceptions = new List<Exception>();
                if (Transaction != null)
                {
                    try
                    {
                        await Transaction.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                try
                {
                    await Connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                if (exceptions.HasValue()) throw new AggregateException($"Could not properly dispose connection to MySql storage", exceptions);
            }
        }
    }
}
