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
using Sels.SQL.QueryBuilder;
using Sels.SQL.QueryBuilder.Builder;
using Sels.SQL.QueryBuilder.Builder.Compilation;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        /// <inheritdoc cref="HiveMindMySqlQueue"/>
        /// <param name="hiveMindOptions">The global hive mind options for this instance</param>
        /// <param name="options">The options for this instance</param>
        /// <param name="environment">The HiveMind environment to interact with</param>
        /// <param name="connectionString">The connection to use to connect to the database</param>
        /// <param name="queryProvider">Provider used to generate queries</param>
        /// <param name="logger">Optional logger for tracing</param>
        public HiveMindMySqlQueue(IOptionsSnapshot<HiveMindOptions> hiveMindOptions, HiveMindMySqlQueueOptions options, string environment, string connectionString, ICachedSqlQueryProvider queryProvider, ILogger? logger = null)
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
        public async Task EnqueueAsync(string queueType, string queue, string jobId, DateTime queueTime, Guid executionId, QueuePriority priority, CancellationToken token = default)
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
                    insert = x.Insert<JobQueueTable>().Into(table: table)
                              .Columns(x => x.Type, x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt)
                              .Parameters(x => x.Type, x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt);
                }
                // No need to include queue type column
                else
                {
                    insert = x.Insert<JobQueueTable>().Into(table: table)
                             .Columns(x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt)
                             .Parameters(x => x.Name, x => x.JobId, x => x.Priority, x => x.ExecutionId, x => x.QueueTime, x => x.EnqueuedAt);
                }

                var select = x.Select().LastInsertedId();

                return x.New().Append(insert).Append(select);
            });
            _logger.Trace($"Inserting job <{jobId}> in queue <{queue}> of type <{queueType}> using query <{query}>");

            // Execute query
            var parameters = new DynamicParameters();
            if(knownQueue == KnownQueueTypes.Unknown) parameters.Add(nameof(JobQueueTable.Type), queueType);
            parameters.Add(nameof(JobQueueTable.Name), queue);
            parameters.Add(nameof(JobQueueTable.JobId), jobId);
            parameters.Add(nameof(JobQueueTable.Priority), priority);
            parameters.Add(nameof(JobQueueTable.ExecutionId), executionId.ToString());
            parameters.Add(nameof(JobQueueTable.QueueTime), queueTime);
            parameters.Add(nameof(JobQueueTable.EnqueuedAt), DateTime.UtcNow);

            long enqueuedId = 0;

            await using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                await using (var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    enqueuedId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(query, parameters, transaction, cancellationToken: token)).ConfigureAwait(false);

                    await transaction.CommitAsync(token).ConfigureAwait(false);
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
