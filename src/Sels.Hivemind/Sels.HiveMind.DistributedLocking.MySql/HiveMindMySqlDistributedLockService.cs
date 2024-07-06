using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Equality;
using Sels.Core.Scope;
using Sels.SQL.QueryBuilder;
using Sels.SQL.QueryBuilder.Builder.Compilation;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.DistributedLocking.MySql
{
    /// <summary>
    /// Places distributed locks using the <see cref="Sels.SQL.QueryBuilder.MySQL.MySql.Functions.GetLock"/> function.
    /// </summary>
    public class HiveMindMySqlDistributedLockService : IDistributedLockService
    {
        // Fields
        private readonly ILogger? _logger;
        private readonly ITaskManager _taskManager;
        private readonly ICachedSqlQueryProvider _queryProvider;
        private readonly IOptionsMonitor<HiveMindOptions> _hiveOptions;
        private readonly string _environment;
        private readonly string _connectionString;

        /// <inheritdoc cref="HiveMindMySqlDistributedLockService"/>
        /// <param name="environment">The environment the service is configured for</param>
        /// <param name="connectionString">The connection string to use to open connections</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlDistributedLockService(string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ITaskManager taskManager, IOptionsMonitor<HiveMindOptions> hiveOptions, ILogger<HiveMindMySqlDistributedLockService>? logger = null)
        {
            _queryProvider = Guard.IsNotNull(queryProvider);
            _taskManager = Guard.IsNotNull(taskManager);
            _hiveOptions = Guard.IsNotNull(hiveOptions);
            _environment = Guard.IsNotNullOrWhitespace(environment);
            _connectionString = Guard.IsNotNullOrWhitespace(connectionString);
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IDistributedLock> AcquireAsync(string resource, string requester, TimeSpan? timeout, CancellationToken token = default)
        {
            resource = Guard.IsNotNullOrWhitespace(resource);
            requester = Guard.IsNotNullOrWhitespace(requester);
            _logger.Log($"Acquiring distributed lock with a timeout of <{timeout}>");

            string query;

            if (timeout.HasValue)
            {
                query = _queryProvider.GetQuery(GetCacheKey($"{nameof(AcquireAsync)}.{timeout.HasValue}"), x =>
                {
                    return x.If().Condition(x => x.GetLock(x => x.Parameter(nameof(resource)), x => x.Parameter(nameof(timeout))).NotEqualTo.Value(1))
                                 .Then(x => x.Signal("DistributedLockTimeout"), false);
                });
            }
            else
            {
                query = _queryProvider.GetQuery(GetCacheKey($"{nameof(AcquireAsync)}.{timeout.HasValue}"), x =>
                {
                    return x.If().Condition(x => x.GetLock(x => x.Parameter(nameof(resource)), x => x.Value(-1.0)).NotEqualTo.Value(1))
                                 .Then(x => x.Signal("DistributedLockTimeout"), false);
                });
            }

            _logger.Trace($"Acquiring distributed lock with a timeout of <{timeout}> using query <{query}>");
            _logger.Debug($"Opening connection to acquire lock");
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add(nameof(resource), $"{_environment}.{resource}");
                if (timeout.HasValue) parameters.Add(nameof(timeout), timeout.Value.TotalSeconds);

                await connection.ExecuteAsync(new CommandDefinition(query, parameters, cancellationToken: token)).ConfigureAwait(false);
                _logger.Log($"Acquired distributed lock");
                return new MySqlDistributedLock(connection, _taskManager, resource, _environment, requester);
            }
            catch(Exception ex)
            {
                _logger.Warning($"Could not acquire distributed lock. Releasing connection", ex);

                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch(Exception innerEx)
                {
                    throw new AggregateException(ex, innerEx);
                }

                throw;
            }
        }
        /// <inheritdoc/>
        public async Task<(bool WasAcquired, IDistributedLock? DistributedLock)> TryAcquireAsync(string resource, string requester, CancellationToken token = default)
        {
            resource = Guard.IsNotNullOrWhitespace(resource);
            requester = Guard.IsNotNullOrWhitespace(requester);
            _logger.Log($"Trying to acquire distributed lock");

            var query = _queryProvider.GetQuery(GetCacheKey(nameof(TryAcquireAsync)), x =>
            {
                return x.Select().ColumnExpression(x => x.GetLock(x => x.Parameter(nameof(resource)), x => x.Value(0.0)));
            });

            _logger.Trace($"Trying to acquire distributed lock using query <{query}>");
            _logger.Debug($"Opening connection to acquire lock");
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add(nameof(resource), $"{_environment}.{resource}");

                var wasAcquired = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(query, parameters, cancellationToken: token)).ConfigureAwait(false);
                if(wasAcquired)
                {
                    _logger.Log($"Acquired distributed lock");
                    return (true, new MySqlDistributedLock(connection, _taskManager, resource, _environment, requester));
                }
                else
                {
                    _logger.Log($"Could not acquire distributed lock");
                    await connection.DisposeAsync().ConfigureAwait(false);
                    return (false, null);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Could not acquire distributed lock. Releasing connection", ex);

                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception innerEx)
                {
                    throw new AggregateException(ex, innerEx);
                }

                throw;
            }
        }

        /// <summary>
        /// Returns the full cache key for <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key to get the full key for</param>
        /// <returns>The full cache key for <paramref name="key"/></returns>
        protected string GetCacheKey(string key)
        {
            key.ValidateArgumentNotNullOrWhitespace(nameof(key));

            return $"{_hiveOptions.Get(_environment).CachePrefix}.{nameof(HiveMindMySqlDistributedLockService)}.{key}";
        }

        private class MySqlDistributedLock : IDistributedLock
        {
            // Fields
            private readonly MySqlConnection _connection;
            private readonly ITaskManager _taskManager;
            private readonly List<Func<Task>> _expiredActions = new List<Func<Task>>();

            // Properties
            /// <inheritdoc/>
            public string Resource { get; }
            /// <inheritdoc/>
            public string Environment { get; }
            /// <inheritdoc/>
            public string Holder { get; }
            /// <inheritdoc/>
            public DateTime ExpectedTimeoutUtc => DateTime.MaxValue;
            /// <inheritdoc/>
            public bool IsSelfManaged => true;

            public MySqlDistributedLock(MySqlConnection connection, ITaskManager taskManager, string resource, string environment, string holder)
            {
                _connection = Guard.IsNotNull(connection);
                _taskManager = Guard.IsNotNull(taskManager);
                Resource = Guard.IsNotNullOrWhitespace(resource);
                Environment = Guard.IsNotNullOrWhitespace(environment);
                Holder = Guard.IsNotNullOrWhitespace(holder);

                _connection.StateChange += OnConnectionChanged;
            }

            private void OnConnectionChanged(object sender, StateChangeEventArgs e)
            {
                if(e.CurrentState.In(ConnectionState.Broken, ConnectionState.Closed))
                    {
                    foreach (var action in _expiredActions)
                    {
                        _taskManager.ScheduleAnonymousAction(action);
                    }
                }
            }

            /// <inheritdoc/>
            public void OnLockExpired(Func<Task> action)
            {
                _expiredActions.Add(Guard.IsNotNull(action));
            }
            /// <inheritdoc/>
            public Task<bool> TryKeepAliveAsync(CancellationToken token = default) => throw new NotSupportedException();

            /// <inheritdoc/>
            public async ValueTask DisposeAsync()
            {
                _connection.StateChange -= OnConnectionChanged;
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
