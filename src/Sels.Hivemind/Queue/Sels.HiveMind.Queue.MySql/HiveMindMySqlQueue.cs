using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Queue.Sql;
using Sels.HiveMind.Storage.MySql;
using Sels.HiveMind.Storage;
using Sels.SQL.QueryBuilder;
using Sels.SQL.QueryBuilder.Builder;
using Sels.SQL.QueryBuilder.Builder.Compilation;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Transactions;
using Sels.Core.Extensions.Linq;
using Sels.SQL.QueryBuilder.Builder.Expressions;
using Sels.SQL.QueryBuilder.Expressions;
using static Sels.HiveMind.HiveMindConstants;
using System.Linq;
using Sels.Core;
using Newtonsoft.Json.Linq;

namespace Sels.HiveMind.Queue.MySql
{
    /// <summary>
    /// Enqueues jobs in MySql tables.
    /// </summary>
    public class HiveMindMySqlQueue : IJobQueue
    {
        // Fields
        private readonly IOptionsSnapshot<HiveMindOptions> _hiveOptions;
        private readonly HiveMindMySqlQueueOptions _options;
        private readonly ICachedSqlQueryProvider _queryProvider;
        private readonly string _environment;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        private readonly ExpressionCompileOptions _compileOptions = ExpressionCompileOptions.AppendSeparator;

        // Properties
        /// <summary>
        /// The name of the job queue table.
        /// </summary>
        protected string DefaultJobQueueTable => $"HiveMind.{_environment}.JobQueue";
        /// <summary>
        /// The name of the queue table that just contains the background jobs to process.
        /// </summary>
        protected string BackgroundJobProcessQueueTable => $"HiveMind.{_environment}.BackgroundJobProcessQueue";
        /// <summary>
        /// The name of the queue table that just contains the background jobs to cleanup.
        /// </summary>
        protected string BackgroundJobCleanupQueueTable => $"HiveMind.{_environment}.BackgroundJobCleanupQueue";
        /// <summary>
        /// The name of the queue table that just contains the recurring jobs to trigger.
        /// </summary>
        protected string RecurringJobTriggerQueueTable => $"HiveMind.{_environment}.RecurringJobTriggerQueue";
        /// <inheritdoc/>
        public JobQueueFeatures Features => JobQueueFeatures.Polling;
        /// <summary>
        /// The options for this instance.
        /// </summary>
        public HiveMindMySqlQueueOptions Options => _options;   

        /// <inheritdoc cref="HiveMindMySqlQueue"/>
        /// <param name="hiveMindOptions">The global hive mind options for this instance</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlQueue(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, HiveMindMySqlQueueOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger logger = null)
        {
            _hiveOptions = hiveMindOptions.ValidateArgument(nameof(hiveMindOptions));
            _options = options.ValidateArgument(nameof(options));
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            _connectionString = connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            _queryProvider = queryProvider.ValidateArgument(nameof(queryProvider)).CreateSubCachedProvider(x => x.WithExpressionCompileOptions(_compileOptions));
            _logger = logger;
        }

        /// <inheritdoc cref="HiveMindMySqlQueue"/>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlQueue(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, HiveMindMySqlQueueOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger<HiveMindMySqlQueue> logger = null) : this(hiveMindOptions, options, environment, connectionString, queryProvider, logger.CastToOrDefault<ILogger>())
        {
        }

        #region Enqueue
        ///<inheritdoc/>
        public async Task EnqueueAsync(string queueType, string queue, string jobId, DateTime queueTime, Guid executionId, QueuePriority priority, IStorageConnection connection, CancellationToken token = default)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            queue.ValidateArgumentNotNullOrWhitespace(nameof(queue));
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));

            _logger.Log($"Inserting job <{jobId}> in queue <{queue}> of type <{queueType}>");
            var knownQueue = ToKnownQueueType(queueType);

            // Generate query
            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(EnqueueAsync)}.{knownQueue}"), x =>
            {
                IQueryBuilder insert = null;
                var table = GetTable(knownQueue);
                if (knownQueue == KnownQueueTypes.Unknown)
                {
                    insert = x.Insert<MySqlJobQueueTable>().Into(table: table)
                              .Columns(x => x.Type, x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt)
                              .Parameters(x => x.Type, x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt);
                }
                // No need to include queue type column
                else
                {
                    insert = x.Insert<MySqlJobQueueTable>().Into(table: table)
                             .Columns(x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt)
                             .Parameters(x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt);
                }

                var select = x.Select().LastInsertedId();

                return x.New().Append(insert).Append(select);
            });
            _logger.Trace($"Inserting job <{jobId}> in queue <{queue}> of type <{queueType}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            if(knownQueue == KnownQueueTypes.Unknown) parameters.Add(nameof(MySqlJobQueueTable.Type), queueType);
            parameters.Add(nameof(MySqlJobQueueTable.Name), queue);
            parameters.Add(nameof(MySqlJobQueueTable.JobId), jobId);
            parameters.Add(nameof(MySqlJobQueueTable.Priority), priority);
            parameters.Add(nameof(MySqlJobQueueTable.ExecutionId), executionId.ToString());
            parameters.Add(nameof(MySqlJobQueueTable.QueueTime), queueTime);
            parameters.Add(nameof(MySqlJobQueueTable.EnqueuedAt), DateTime.UtcNow);

            long enqueuedId = 0;

            if(TryGetStorageConnection(connection, out var mySqlConnection))
            {
                enqueuedId = await mySqlConnection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, mySqlConnection.Transaction, cancellationToken: token)).ConfigureAwait(false);
            }
            else
            {
                await using (var mySqlconnection = new MySqlConnection(_connectionString))
                {
                    await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                    await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                    {
                        enqueuedId = await mySqlconnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false);

                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }
                }
            }
            
            _logger.Log($"Inserting job <{jobId}> in queue <{queue}> of type <{queueType}>. Enqueued job record has id <{enqueuedId}>");
        }

        /// <summary>
        /// Parses <paramref name="queueType"/> to <see cref="KnownQueueTypes"/>.
        /// </summary>
        /// <param name="queueType">The queue type to parse</param>
        /// <returns>The known queue type parsed from <paramref name="queueType"/></returns>
        protected KnownQueueTypes ToKnownQueueType(string queueType)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));

            if (queueType.EqualsNoCase(HiveMindConstants.Queue.BackgroundJobProcessQueueType)) return KnownQueueTypes.BackgroundJobProcess;
            else if (queueType.EqualsNoCase(HiveMindConstants.Queue.BackgroundJobCleanupQueueType)) return KnownQueueTypes.BackgroundJobCleanup;
            else if (queueType.EqualsNoCase(HiveMindConstants.Queue.RecurringJobTriggerQueueType)) return KnownQueueTypes.RecurringJobTrigger;

            return KnownQueueTypes.Unknown;
        }

        /// <summary>
        /// Returns the name of the table used for <paramref name="knownQueue"/>.
        /// </summary>
        /// <param name="knownQueue">The known queue type to get the table name for</param>
        /// <returns>The table name for known queue <paramref name="knownQueue"/></returns>
        protected string GetTable(KnownQueueTypes knownQueue)
        {
            switch(knownQueue)
            {
                case KnownQueueTypes.BackgroundJobProcess: return BackgroundJobProcessQueueTable;
                case KnownQueueTypes.BackgroundJobCleanup: return BackgroundJobCleanupQueueTable;
                case KnownQueueTypes.RecurringJobTrigger: return RecurringJobTriggerQueueTable;
                default: return DefaultJobQueueTable;
            }
        }
        #endregion

        #region Dequeue
        /// <inheritdoc/>
        public async Task<IDequeuedJob[]> DequeueAsync(string queueType, IEnumerable<string> queues, int amount, CancellationToken token = default)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            queues.ValidateArgumentNotNullOrEmpty(nameof(queues));
            amount.ValidateArgumentLargerOrEqual(nameof(amount), 1);

            _logger.Log($"Dequeueing the next <{amount}> jobs from queues <{queues.JoinString()}> of type <{queueType}>");

            var knownQueue = ToKnownQueueType(queueType);
            var processId = Guid.NewGuid().ToString();
            var table = GetTable(knownQueue);

            // Generate query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(queueType), queueType);
            parameters.Add(nameof(amount), amount);
            queues.Execute((i, x) => parameters.Add($"{nameof(queues)}{i}", x));
            parameters.Add(nameof(processId), processId);

            var selectIdQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(DequeueAsync)}.SelectToUpdate.{knownQueue}.{amount}"), x => {              
                var select = x.Select<MySqlJobQueueTable>().Column(x => x.Id)
                              .From(table, typeof(MySqlJobQueueTable))
                              .Where(x => x.Column(x => x.QueueTime).LesserOrEqualTo.CurrentDate(DateType.Server).And
                                           .Column(x => x.Name).In.Parameters(queues.Select((x, i) => $"{nameof(queues)}{i}")).And
                                           .Column(x => x.FetchedAt).IsNull)
                              .OrderBy(x => x.Priority, SortOrders.Ascending).OrderBy(x => x.QueueTime, SortOrders.Ascending)
                              .Limit(new ParameterExpression(nameof(amount)));
                
                if (knownQueue == KnownQueueTypes.Unknown)
                {
                    select = select.Where(x => x.Column(x => x.Type).EqualTo.Parameter(nameof(queueType)));
                }
                return select;
            });
            _logger.Trace($"Select the ids of the next <{amount}> jobs from queues <{queues.JoinString()}> of type <{queueType}> using query <{selectIdQuery}>");

            // Execute query
            MySqlJobQueueTable[] dequeued = null;
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    var ids = (await mySqlconnection.QueryAsync<long>(new CommandDefinition(selectIdQuery, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

                    if (!ids.HasValue())
                    {
                        _logger.Log($"Queues <{queues.JoinString()}> of type <{queueType}> are empty. Nothing to dequeue");
                        return Array.Empty<IDequeuedJob>();
                    }

                    var updateAndSelectQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(DequeueAsync)}.UpdateAndSelect.{ids.Length}"), x =>
                    {
                        var update = x.Update<MySqlJobQueueTable>().Table(table, typeof(MySqlJobQueueTable))
                                      .Set.Column(x => x.ProcessId).To.Parameter(nameof(processId))
                                      .Set.Column(x => x.FetchedAt).To.CurrentDate(DateType.Server)
                                      .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"{nameof(ids)}{i}")));

                        var select = x.Select<MySqlJobQueueTable>().From(table, typeof(MySqlJobQueueTable))
                                      .Where(x => x.Column(x => x.ProcessId).EqualTo.Parameter(nameof(processId)));

                        return x.New().Append(update).Append(select);
                    });

                    ids.Execute((i, x) => parameters.Add($"{nameof(ids)}{i}", x));
                    _logger.Trace($"Updating <{amount}> jobs with process lock from queues <{queues.JoinString()}> of type <{queueType}> using query <{updateAndSelectQuery}>");
                    dequeued = (await mySqlconnection.QueryAsync<MySqlJobQueueTable>(new CommandDefinition(updateAndSelectQuery, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Dequeued <{dequeued?.Length}> jobs from queues <{queues.JoinString()}> of type <{queueType}>");
            return dequeued.Select(x => new MySqlDequeuedJob(this, x, queueType)).ToArray();
        }
        #endregion

        /// <summary>
        /// Unlocks a maximum of <paramref name="limit"/> jobs if they are locked and their last heartbeat was <paramref name="timeout"/> time ago.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many timed out jobs were unlocked</returns>
        public async Task<int> UnlockExpiredAsync(CancellationToken token)
        {
            _logger.Log($"Trying to unlock timed out jobs");

            int unlocked = 0;
            var knownQueue = Helper.Enums.GetAll<KnownQueueTypes>();
            var limit = _options.UnlockBatchSize;
            var timeout = _options.LockTimeout;

            var parameters = new DynamicParameters();
            parameters.Add(nameof(limit), limit);
            parameters.Add(nameof(timeout), -timeout.TotalMilliseconds);
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);

                foreach (var queueType in knownQueue)
                {
                    bool anyLeft = true;
                    var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(UnlockExpiredAsync)}.{queueType}"), x =>
                    {
                        var table = GetTable(queueType);

                        return x.Update<MySqlJobQueueTable>().Table(table, typeof(MySqlJobQueueTable))
                                .Set.Column(x => x.ProcessId).To.Null()
                                .Set.Column(x => x.FetchedAt).To.Null()
                                .Where(x => x.Column(x => x.FetchedAt).LesserThan.ModifyDate(x => x.CurrentDate(DateType.Server), x => x.Parameter(nameof(timeout)), DateInterval.Millisecond))
                                .Limit(new ParameterExpression(nameof(limit)));
                    });
                    _logger.Trace($"Trying to unlock timed out jobs for queue type <{queueType}> using query <{query}>");

                    while (anyLeft)
                    {
                        _logger.Debug($"Trying to unlock the next <{limit}> timed out jobs for queue type <{queueType}>");
                        await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                        {
                            var released = await mySqlconnection.ExecuteAsync(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false);

                            _logger.Debug($"Unlocked <{released}> timed out jobs for queue type <{queueType}>");

                            anyLeft = released >= limit;
                            unlocked += released;

                            await transaction.CommitAsync(token).ConfigureAwait(false);
                        }
                    }
                }
            }

            return unlocked;
        }

        /// <summary>
        /// Parses <paramref name="connection"/> as <see cref="MySqlStorageConnection"/>.
        /// </summary>
        /// <param name="connection">The connection to parse</param>
        /// <returns>The connection parsed from <paramref name="connection"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        protected bool TryGetStorageConnection(IStorageConnection connection, out MySqlStorageConnection mySqlConnection)
        {
            connection.ValidateArgument(nameof(connection));
            mySqlConnection = null;

            if (connection is MySqlStorageConnection storageConnection)
            {
                if (!storageConnection.Environment.EqualsNoCase(_environment)) throw new InvalidOperationException($"Storage connection was opened for environment <{storageConnection.Environment}> but storage is configured for <{_environment}>");

                mySqlConnection = storageConnection;
                return true;
            }

            return true;
        }

        /// <summary>
        /// Returns the full cache key for <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key to get the full key for</param>
        /// <returns>The full cache key for <paramref name="key"/></returns>
        protected string GetCacheKey(string key)
        {
            key.ValidateArgumentNotNullOrWhitespace(nameof(key));

            return $"{_hiveOptions.Value.CachePrefix}.{nameof(HiveMindMySqlQueue)}.{key}";
        }

        /// <summary>
        /// Enum that contains the known queue types.
        /// </summary>
        protected enum KnownQueueTypes
        {
            BackgroundJobProcess = 0,
            BackgroundJobCleanup = 1,
            RecurringJobTrigger = 2,
            Unknown = 3
        }
    }
}
