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
using System.Linq;
using Sels.Core;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Sels.HiveMind.Queue.MySql
{
    /// <summary>
    /// Enqueues jobs in MySql tables.
    /// </summary>
    public class HiveMindMySqlQueue : IJobQueue
    {
        // Fields
        private readonly IOptionsMonitor<HiveMindOptions> _hiveOptions;
        private readonly IOptionsMonitor<HiveMindMySqlQueueOptions> _options;
        private readonly ICachedSqlQueryProvider _queryProvider;
        private readonly string _environment;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        private readonly ExpressionCompileOptions _compileOptions = ExpressionCompileOptions.AppendSeparator;

        // Properties       
        /// <inheritdoc/>
        public JobQueueFeatures Features => JobQueueFeatures.Polling;
        /// <summary>
        /// The options for this instance.
        /// </summary>
        public HiveMindMySqlQueueOptions Options => _options.Get(_environment);   
        /// <summary>
        /// The HiveMind environment the current queue is configured for.
        /// </summary>
        public string Environment => _environment;
        private QueueTableNames TableNames { get; }

        /// <inheritdoc cref="HiveMindMySqlQueue"/>
        /// <param name="hiveMindOptions">The global hive mind options for this instance</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlQueue(IOptionsMonitor<HiveMindOptions> hiveMindOptions, IOptionsMonitor<HiveMindMySqlQueueOptions> options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger logger = null)
        {
            _hiveOptions = hiveMindOptions.ValidateArgument(nameof(hiveMindOptions));
            _options = options.ValidateArgument(nameof(options));
            _environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            _connectionString = connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString));
            _queryProvider = queryProvider.ValidateArgument(nameof(queryProvider)).CreateSubCachedProvider(x => x.WithExpressionCompileOptions(_compileOptions));
            _logger = logger;
            TableNames = new QueueTableNames(environment);
        }

        /// <inheritdoc cref="HiveMindMySqlQueue"/>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlQueue(IOptionsMonitor<HiveMindOptions> hiveMindOptions, IOptionsMonitor<HiveMindMySqlQueueOptions> options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger<HiveMindMySqlQueue> logger = null) : this(hiveMindOptions, options, environment, connectionString, queryProvider, logger.CastToOrDefault<ILogger>())
        {
        }

        /// <summary>
        /// Proxy contructor.
        /// </summary>
        protected HiveMindMySqlQueue()
        {
            
        }

        #region Enqueue
        ///<inheritdoc/>
        public virtual async Task EnqueueAsync(string queueType, string queue, string jobId, DateTime queueTime, Guid executionId, QueuePriority priority, IStorageConnection connection, CancellationToken token = default)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            queue.ValidateArgumentNotNullOrWhitespace(nameof(queue));
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));

            _logger.Log($"Inserting job <{HiveLog.Job.Id}> in queue <{HiveLog.Job.Queue}> of type <{HiveLog.Job.QueueType}>", jobId, queue, queueType);
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
            _logger.Trace($"Inserting job <{HiveLog.Job.Id}> in queue <{HiveLog.Job.Queue}> of type <{HiveLog.Job.QueueType}> using query <{query}>", jobId, queue, queueType);

            // Execute query
            var parameters = new DynamicParameters();
            if(knownQueue == KnownQueueTypes.Unknown) parameters.Add(nameof(MySqlJobQueueTable.Type), queueType);
            parameters.Add(nameof(MySqlJobQueueTable.Name), queue);
            parameters.Add(nameof(MySqlJobQueueTable.JobId), jobId);
            parameters.Add(nameof(MySqlJobQueueTable.Priority), priority);
            parameters.Add(nameof(MySqlJobQueueTable.ExecutionId), executionId.ToString());
            parameters.Add(nameof(MySqlJobQueueTable.QueueTime), queueTime.ToUniversalTime());
            parameters.Add(nameof(MySqlJobQueueTable.EnqueuedAt), DateTime.UtcNow);

            long enqueuedId = 0;

            if(connection is MySqlStorageConnection storageConnection)
            {
                enqueuedId = await storageConnection.MySqlConnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, storageConnection.MySqlTransaction, cancellationToken: token)).ConfigureAwait(false);
            }
            else
            {
                await using (var mySqlconnection = new MySqlConnection(_connectionString))
                {
                    await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                    enqueuedId = await mySqlconnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, cancellationToken: token)).ConfigureAwait(false);
                }
            }
            

            _logger.Log($"Inserting job <{HiveLog.Job.Id}> in queue <{HiveLog.Job.Queue}> of type <{HiveLog.Job.QueueType}>. Enqueued job record has id <{enqueuedId}>", jobId, queue, queueType);
        }
        #endregion

        public virtual async Task<long> GetQueueLengthAsync(string queueType, string queue, CancellationToken token = default)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            queue.ValidateArgumentNotNullOrWhitespace(nameof(queue));

            _logger.Log($"Counting the amount of jobs in queue <{HiveLog.Job.Queue}> of type <{HiveLog.Job.QueueType}>", queue, queueType);
            var knownQueue = ToKnownQueueType(queueType);

            // Generate query 
            var query = _queryProvider.GetQuery($"{nameof(GetQueueLengthAsync)}.{knownQueue}", x =>
            {
                var table = GetTable(knownQueue);
                return x.Select<MySqlJobQueueTable>().CountAll().From(table, typeof(MySqlJobQueueTable)).Where(x => x.Column(x => x.Name).EqualTo.Parameter(nameof(queue)));
            });
            _logger.Trace($"Counting the amount of jobs in queue <{HiveLog.Job.Queue}> of type <{HiveLog.Job.QueueType}> using query <{query}>", queue, queueType);
            var parameters = new DynamicParameters();
            parameters.Add(nameof(queue), queue);

            long amount = 0;
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                amount = await mySqlconnection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, cancellationToken: token)).ConfigureAwait(false);
            }

            _logger.Log($"Counted <{amount}> jobs in queue <{HiveLog.Job.Queue}> of type <{HiveLog.Job.QueueType}>", queue, queueType);
            return amount;
        }

        #region Dequeue
        /// <inheritdoc/>
        public virtual async Task<IDequeuedJob[]> DequeueAsync(string queueType, IEnumerable<string> queues, int amount, CancellationToken token = default)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));
            queues.ValidateArgumentNotNullOrEmpty(nameof(queues));
            amount.ValidateArgumentLargerOrEqual(nameof(amount), 1);

            _logger.Log($"Dequeueing the next <{amount}> jobs from queues <{queues.JoinString()}> of type <{HiveLog.Job.QueueType}>", queueType);

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
                              .From(table, typeof(MySqlJobQueueTable)).ForUpdateSkipLocked()
                              .Where(x => x.Column(x => x.QueueTime).LesserOrEqualTo.CurrentDate(DateType.Utc).And
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
            _logger.Trace($"Select the ids of the next <{amount}> jobs from queues <{queues.JoinString()}> of type <{HiveLog.Job.QueueType}> using query <{selectIdQuery}>", queueType);

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
                        _logger.Log($"Queues <{queues.JoinString()}> of type <{HiveLog.Job.QueueType}> are empty. Nothing to dequeue", queueType);
                        return Array.Empty<IDequeuedJob>();
                    }

                    var updateAndSelectQuery = _queryProvider.GetQuery(GetCacheKey($"{nameof(DequeueAsync)}.UpdateAndSelect.{knownQueue}.{ids.Length}"), x =>
                    {
                        var update = x.Update<MySqlJobQueueTable>().Table(table, typeof(MySqlJobQueueTable))
                                      .Set.Column(x => x.ProcessId).To.Parameter(nameof(processId))
                                      .Set.Column(x => x.FetchedAt).To.CurrentDate(DateType.Utc)
                                      .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"{nameof(ids)}{i}")));

                        var select = x.Select<MySqlJobQueueTable>().From(table, typeof(MySqlJobQueueTable))
                                      .Where(x => x.Column(x => x.Id).In.Parameters(ids.Select((x, i) => $"{nameof(ids)}{i}")));

                        return x.New().Append(update).Append(select);
                    });

                    ids.Execute((i, x) => parameters.Add($"{nameof(ids)}{i}", x));
                    _logger.Trace($"Updating <{amount}> jobs with process lock from queues <{queues.JoinString()}> of type <{HiveLog.Job.QueueType}> using query <{updateAndSelectQuery}>", queueType);
                    dequeued = (await mySqlconnection.QueryAsync<MySqlJobQueueTable>(new CommandDefinition(updateAndSelectQuery, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Dequeued <{dequeued?.Length}> jobs from queues <{queues.JoinString()}> of type <{HiveLog.Job.QueueType}>", queueType);
            return dequeued.Select(x => new MySqlDequeuedJob(this, x, queueType)).ToArray();
        }
        #endregion

        /// <summary>
        /// Tries to extend the lock heartbeat on dequeued job <paramref name="id"/> in queue of type <paramref name="queueType"/> if it is still held by process <paramref name="processId"/>.
        /// </summary>
        /// <param name="id">The id of the dequeued job to update</param>
        /// <param name="queueType">The type of the queue the job is placed in</param>
        /// <param name="processId">The id of the process who is supposed to hold the job</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if dequeued job <paramref name="id"/> was updated, otherwise false</returns>
        public virtual async Task<bool> TrySetHeartbeatAsync(long id, string queueType, string processId, CancellationToken token)
        {
            id.ValidateArgumentLarger(nameof(id), 0);
            processId.ValidateArgumentNotNullOrWhitespace(nameof(processId));

            _logger.Log($"Trying to set the lock heartbeat on dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}>", id, queueType);
            // Generate query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(queueType), queueType);
            parameters.Add(nameof(processId), processId);
            var knownQueue = ToKnownQueueType(queueType);

            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TrySetHeartbeatAsync)}.{knownQueue}"), x =>
            {
                var table = GetTable(knownQueue);
                return x.Update<MySqlJobQueueTable>().Table(table, typeof(MySqlJobQueueTable))
                        .Set.Column(x => x.FetchedAt).To.CurrentDate(DateType.Utc)
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(x => x.Id).And.Column(x => x.ProcessId).EqualTo.Parameter(x => x.ProcessId));
            });
            _logger.Trace($"Trying to set the lock heartbeat on dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}> using query <{query}>", id, queueType);

            // Execute query
            bool wasUpdated = false;
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    wasUpdated = (await mySqlconnection.ExecuteAsync(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)) == 1;
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Setting the lock heartbeat on dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}> was <{wasUpdated}>", id, queueType);
            return wasUpdated;
        }
        /// <summary>
        /// Tries to return dequeued job <paramref name="id"/> to queue of type <paramref name="queueType"/> if it is still held by process <paramref name="processId"/>.
        /// </summary>
        /// <param name="id">The id of the dequeued job to return</param>
        /// <param name="queueType">The type of the queue the job is placed in</param>
        /// <param name="processId">The id of the process who is supposed to hold the job</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if dequeued job <paramref name="id"/> was returned to the queue, otherwise false</returns>
        public virtual async Task<bool> TryReleaseAsync(long id, string queueType, string processId, CancellationToken token)
        {
            id.ValidateArgumentLarger(nameof(id), 0);
            processId.ValidateArgumentNotNullOrWhitespace(nameof(processId));

            _logger.Log($"Trying to return dequeued job <{HiveLog.Job.Id}> to queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}>", id, queueType);
            // Generate query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(queueType), queueType);
            parameters.Add(nameof(processId), processId);
            var knownQueue = ToKnownQueueType(queueType);

            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TryReleaseAsync)}.{knownQueue}"), x =>
            {
                var table = GetTable(knownQueue);
                return x.Update<MySqlJobQueueTable>().Table(table, typeof(MySqlJobQueueTable))
                        .Set.Column(x => x.FetchedAt).To.Null()
                        .Set.Column(x => x.ProcessId).To.Null()
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(x => x.Id).And.Column(x => x.ProcessId).EqualTo.Parameter(x => x.ProcessId));
            });
            _logger.Trace($"Trying to return dequeued job <{HiveLog.Job.Id}> to queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}> using query <{query}>", id, queueType);

            // Execute query
            bool wasUpdated = false;
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    wasUpdated = (await mySqlconnection.ExecuteAsync(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)) == 1;
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Returning of dequeued job <{HiveLog.Job.Id}> to queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}> was <{wasUpdated}>", id, queueType);
            return wasUpdated;
        }
        /// <summary>
        /// Tries to delete dequeued job <paramref name="id"/> in queue of type <paramref name="queueType"/> if it is still held by process <paramref name="processId"/>.
        /// </summary>
        /// <param name="id">The id of the dequeued job to delete</param>
        /// <param name="queueType">The type of the queue the job is placed in</param>
        /// <param name="processId">The id of the process who is supposed to hold the job</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if dequeued job <paramref name="id"/> was deleted, otherwise false</returns>
        public virtual async Task<bool> TryDeleteAsync(long id, string queueType, string processId, CancellationToken token)
        {
            id.ValidateArgumentLarger(nameof(id), 0);
            processId.ValidateArgumentNotNullOrWhitespace(nameof(processId));

            _logger.Log($"Trying to delete dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}>", id, queueType);
            // Generate query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(queueType), queueType);
            parameters.Add(nameof(processId), processId);
            var knownQueue = ToKnownQueueType(queueType);

            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TryDeleteAsync)}.{knownQueue}"), x =>
            {
                var table = GetTable(knownQueue);
                return x.Delete<MySqlJobQueueTable>().From(table, typeof(MySqlJobQueueTable))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(x => x.Id).And.Column(x => x.ProcessId).EqualTo.Parameter(x => x.ProcessId));
            });
            _logger.Trace($"Trying to delete dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}> using query <{query}>", id, queueType);

            // Execute query
            bool wasDeleted = false;
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    wasDeleted = (await mySqlconnection.ExecuteAsync(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)) == 1;
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Deletion of dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> if it is still held by <{processId}> was <{wasDeleted}>", id, queueType);
            return wasDeleted;
        }
        /// <summary>
        /// Tries to delay dequeued job <paramref name="id"/> in queue of type <paramref name="queueType"/> to be picked again after <paramref name="delayToUtc"/> if it is still held by process <paramref name="processId"/>.
        /// </summary>
        /// <param name="id">The id of the dequeued job to delay</param>
        /// <param name="queueType">The type of the queue the job is placed in</param>
        /// <param name="processId">The id of the process who is supposed to hold the job</param>
        /// <param name="delayToUtc">The date (in utc) after which the job can be picked up again</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if dequeued job <paramref name="id"/> was delayed, otherwise false</returns>
        public virtual async Task<bool> TryDelayToAsync(long id, string queueType, string processId, DateTime delayToUtc, CancellationToken token)
        {
            id.ValidateArgumentLarger(nameof(id), 0);
            processId.ValidateArgumentNotNullOrWhitespace(nameof(processId));

            _logger.Log($"Trying to delay dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> to <{delayToUtc}> if it is still held by <{processId}>", id, queueType);
            // Generate query
            var parameters = new DynamicParameters();
            parameters.Add(nameof(id), id);
            parameters.Add(nameof(queueType), queueType);
            parameters.Add(nameof(processId), processId);
            parameters.Add(nameof(delayToUtc), delayToUtc.ToUniversalTime());
            var knownQueue = ToKnownQueueType(queueType);

            var query = _queryProvider.GetQuery(GetCacheKey($"{nameof(TryDelayToAsync)}.{knownQueue}"), x =>
            {
                var table = GetTable(knownQueue);
                return x.Update<MySqlJobQueueTable>().Table(table, typeof(MySqlJobQueueTable))
                        .Set.Column(x => x.FetchedAt).To.Null()
                        .Set.Column(x => x.ProcessId).To.Null()
                        .Set.Column(x => x.QueueTime).To.Parameter(nameof(delayToUtc))
                        .Where(x => x.Column(x => x.Id).EqualTo.Parameter(x => x.Id).And.Column(x => x.ProcessId).EqualTo.Parameter(x => x.ProcessId));
            });
            _logger.Trace($"Trying to delay dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> to <{delayToUtc}> if it is still held by <{processId}> using query <{query}>", id, queueType);

            // Execute query
            bool wasUpdated = false;
            await using (var mySqlconnection = new MySqlConnection(_connectionString))
            {
                await mySqlconnection.OpenAsync(token).ConfigureAwait(false);
                await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    wasUpdated = (await mySqlconnection.ExecuteAsync(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false)) == 1;
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            _logger.Log($"Delaying of dequeued job <{HiveLog.Job.Id}> in queue of type <{HiveLog.Job.QueueType}> to <{delayToUtc}> if it is still held by <{processId}> was <{wasUpdated}>", id, queueType);
            return wasUpdated;
        }

        /// <summary>
        /// Unlocks jobs where the lock was timed out.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>How many timed out jobs were unlocked</returns>
        public virtual async Task<int> UnlockExpiredAsync(CancellationToken token)
        {
            _logger.Log($"Trying to unlock timed out jobs");

            int unlocked = 0;
            var knownQueue = Helper.Enums.GetAll<KnownQueueTypes>();
            var limit = Options.UnlockBatchSize;
            var timeout = Options.LockTimeout;

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
                                .Where(x => x.Column(x => x.FetchedAt).LesserThan.ModifyDate(x => x.CurrentDate(DateType.Utc), x => x.Parameter(nameof(timeout)), DateInterval.Millisecond))
                                .Limit(new ParameterExpression(nameof(limit)));
                    });
                    _logger.Trace($"Trying to unlock timed out jobs for queue type <{HiveLog.Job.QueueType}> using query <{query}>", queueType);

                    while (anyLeft)
                    {
                        _logger.Debug($"Trying to unlock the next <{limit}> timed out jobs for queue type <{HiveLog.Job.QueueType}>", queueType);
                        await using (var transaction = await mySqlconnection.BeginTransactionAsync(token).ConfigureAwait(false))
                        {
                            var released = await mySqlconnection.ExecuteAsync(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false);

                            _logger.Debug($"Unlocked <{released}> timed out jobs for queue type <{HiveLog.Job.QueueType}>", queueType);

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
        /// Parses <paramref name="queueType"/> to <see cref="KnownQueueTypes"/>.
        /// </summary>
        /// <param name="queueType">The queue type to parse</param>
        /// <returns>The known queue type parsed from <paramref name="queueType"/></returns>
        protected KnownQueueTypes ToKnownQueueType(string queueType)
        {
            queueType.ValidateArgumentNotNullOrWhitespace(nameof(queueType));

            if (queueType.EqualsNoCase(HiveMindConstants.Queue.BackgroundJobProcessQueueType)) return KnownQueueTypes.BackgroundJobProcess;
            else if (queueType.EqualsNoCase(HiveMindConstants.Queue.RecurringJobProcessQueueType)) return KnownQueueTypes.RecurringJobTrigger;

            return KnownQueueTypes.Unknown;
        }

        /// <summary>
        /// Returns the name of the table used for <paramref name="knownQueue"/>.
        /// </summary>
        /// <param name="knownQueue">The known queue type to get the table name for</param>
        /// <returns>The table name for known queue <paramref name="knownQueue"/></returns>
        protected string GetTable(KnownQueueTypes knownQueue)
        {
            switch (knownQueue)
            {
                case KnownQueueTypes.BackgroundJobProcess: return TableNames.BackgroundJobProcessQueueTable;
                case KnownQueueTypes.RecurringJobTrigger: return TableNames.RecurringJobProcessQueueTable;
                default: return TableNames.GenericJobQueueTable;
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

            return $"{_hiveOptions.Get(_environment).CachePrefix}.{nameof(HiveMindMySqlQueue)}.{key}";
        }

        /// <summary>
        /// Enum that contains the known queue types.
        /// </summary>
        protected enum KnownQueueTypes
        {
            BackgroundJobProcess = 0,
            RecurringJobTrigger = 1,
            Unknown = 2
        }
    }
}
