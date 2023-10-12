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

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Persists HiveMind state in a MySql database.
    /// </summary>
    public class HiveMindMySqlStorage : IStorage
    {
        // Fields
        private readonly IOptions<HiveMindOptions> _hiveOptions;
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
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptions<HiveMindOptions> hiveMindOptions, HiveMindMySqlStorageOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger? logger = null)
        {
            _hiveOptions = hiveMindOptions.ValidateArgument(nameof(hiveMindOptions));
            _options = options.ValidateArgument(nameof(options));
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            _connectionString = connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            _queryProvider = queryProvider.ValidateArgument(nameof(queryProvider));
            _logger = logger;
        }

        /// <inheritdoc cref="HiveMindMySqlStorage"/>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlStorage(IOptions<HiveMindOptions> hiveMindOptions, HiveMindMySqlStorageOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger<HiveMindMySqlStorage>? logger = null) : this(hiveMindOptions, options, environment, connectionString, queryProvider, logger.CastToOrDefault<ILogger>())
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
                return storageConnection;
            }

            throw new InvalidOperationException($"Expected connection to be of type <{typeof(MySqlStorageConnection)}> but got <{connection}>");
        }

        /// <inheritdoc/>
        public virtual async Task<string> CreateJobAsync(JobStorageData jobData, IStorageConnection connection, CancellationToken token = default)
        {
            jobData.ValidateArgument(nameof(jobData));
            var storageConnection = GetStorageConnection(connection);

            _logger.Log($"Inserting new background job in environment <{_environment}>");
            // Job
            var job = new MySqlBackgroundJobTable(jobData);
            var query = _queryProvider.GetQuery(GetCacheKey(nameof(CreateJobAsync)), x =>
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
                var properties = jobData.Properties.Select(x => new BackgroundJobPropertyTable(x));
                await InsertPropertiesAsync(storageConnection, id, properties, token).ConfigureAwait(false);
            }
            _logger.Log($"Inserted background job <{id}> in environment <{_environment}>");
            return id.ToString();
        }
        /// <inheritdoc/>
        public virtual Task<string> UpdateJobAsync(JobStorageData jobData, IStorageConnection connection, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public virtual Task<bool> DeleteJobAsync(string id, IStorageConnection connection, CancellationToken token = default)
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

            _logger.Log($"Inserting new states for background job <{backgroundJobId}>");

            // Reset is current on existing states
            var resetQuery = _queryProvider.Update<StateTable>()
                                           .Set.Column(c => c.IsCurrent).To.Value(false)
                                           .Where(w => w.Column(c => c.BackgroundJobId).EqualTo.Value(backgroundJobId))
                                           .Build(_compileOptions);
            _logger.Trace($"Resetting {nameof(StateTable.IsCurrent)} to false for existing states using query <{resetQuery}>");

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

            _logger.Log($"Inserting new state properties");

            var parameters = new DynamicParameters();
            var query = _queryProvider.Insert<StatePropertyTable>().Into(table: BackgroundJobStatePropertyTable)
                                      .From(parameters, properties, c => c.StateId, c => c.Name, c => c.Type, c => c.OriginalType, c => c.TextValue, c => c.NumberValue, c => c.FloatingNumberValue, c => c.DateValue, c => c.OtherValue)
                                      .Build(_compileOptions);
            _logger.Trace($"Inserting new state properties using query <{query}>");

            var inserted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);

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
        protected virtual async Task InsertPropertiesAsync(MySqlStorageConnection connection, long backgroundJobId, IEnumerable<BackgroundJobPropertyTable> properties, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            backgroundJobId.ValidateArgumentLarger(nameof(backgroundJobId), 0);
            properties.ValidateArgumentNotNullOrEmpty(nameof(properties));

            _logger.Log($"Inserting new properties for background job <{backgroundJobId}>");
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
            _logger.Trace($"Inserting new properties for background job <{backgroundJobId}> using query <{query}>");

            var inserted = await connection.Connection.ExecuteAsync(new CommandDefinition(query, parameters, connection.Transaction, cancellationToken: token)).ConfigureAwait(false);

            _logger.Log($"Inserted <{inserted}> new properties for background job <{backgroundJobId}>");
        }

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
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1,1);
        private readonly List<Delegates.Async.AsyncAction<CancellationToken>> _commitActions = new List<Delegates.Async.AsyncAction<CancellationToken>>();

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
                if(Transaction == null)
                {
                    Transaction = await Connection.BeginTransactionAsync(token).ConfigureAwait(false);
                }
            }
        }
        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                Delegates.Async.AsyncAction<CancellationToken>[]? actions = null;
                lock (_commitActions)
                {
                    actions = _commitActions.ToArray();
                    _commitActions.Clear();
                }

                foreach(var action in  actions)
                {
                    await action(token).ConfigureAwait(false);
                }

                if(Transaction != null) await Transaction.CommitAsync(token).ConfigureAwait(false);
                Transaction = null;
            }
        }
        /// <inheritdoc/>
        public void OnCommitting(Delegates.Async.AsyncAction<CancellationToken> action)
        {
            action.ValidateArgument(nameof(action));

            lock(_commitActions)
            {
                _commitActions.Add(action);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using (await _lock.LockAsync().ConfigureAwait(false))
            {
                var exceptions = new List<Exception>();
                if(Transaction != null)
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
