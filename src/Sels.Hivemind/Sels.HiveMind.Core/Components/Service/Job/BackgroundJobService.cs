using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind.Components;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;
using Expression = System.Linq.Expressions.Expression;

namespace Sels.HiveMind.Service.Job
{
    /// <inheritdoc cref="IBackgroundJobService"/>
    public class BackgroundJobService : IBackgroundJobService
    {
        // Fields
        private readonly IOptions<HiveMindOptions> _options;
        private readonly IMemoryCache _cache;
        private readonly BackgroundJobValidationProfile _backgroundJobValidationProfile;
        private readonly ILogger _logger;

        /// <inheritdoc cref="BackgroundJobService"/>
        /// <param name="backgroundJobValidationProfile">Used to validate background jobs</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobService(IOptions<HiveMindOptions> options, IMemoryCache cache, BackgroundJobValidationProfile backgroundJobValidationProfile, ILogger<BackgroundJobService> logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache.ValidateArgument(nameof(cache));
            _backgroundJobValidationProfile = backgroundJobValidationProfile.ValidateArgument(nameof(backgroundJobValidationProfile));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> StoreAsync(IStorageConnection connection, JobStorageData job, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            job.ValidateArgument(nameof(job));

            // Validate storage data
            var result = await _backgroundJobValidationProfile.ValidateAsync(job, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.DisplayName}: {x.Message}").ThrowOnValidationErrors(job);

            return await RunTransaction(connection, async () =>
            {
                // Update
                if (job.Id.HasValue())
                {
                    throw new NotImplementedException();
                }
                // Create
                else
                {
                    _logger.Log($"Creating new job in environment <{connection.Environment}>");
                    var id = await connection.Storage.CreateJobAsync(job, connection, token).ConfigureAwait(false);
                    _logger.Log($"Created job <{id}> in environment <{connection.Environment}>");
                    return id;
                }
            }, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task<JobStorageData> ConvertToStorageFormatAsync(IReadOnlyBackgroundJob job, CancellationToken token = default)
        {
            job.ValidateArgument(nameof(job));

            // Validate state before persisting just to be sure
            var result = await _backgroundJobValidationProfile.ValidateAsync(job, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.DisplayName}: {x.Message}").ThrowOnValidationErrors(job);

            // Convert to storage
            var jobStorage = new JobStorageData(job);
            if (job.StateHistory.HasValue())
            {
                foreach (var state in job.StateHistory)
                {
                    var historyProperties = GetStorageProperties(state);
                    var stateHistoryStorage = new JobStateStorageData(state, historyProperties);
                    jobStorage.AddState(stateHistoryStorage, job.ChangeTracker.NewStates.Contains(state));
                }
            }
            var properties = GetStorageProperties(job.State);
            var stateStorage = new JobStateStorageData(job.State, properties);
            jobStorage.AddState(stateStorage, job.ChangeTracker.NewStates.Contains(job.State));

            return jobStorage;
        }

        private IEnumerable<StorageProperty> GetStorageProperties(IBackgroundJobState state)
        {
            state.ValidateArgument(nameof(state));

            var cacheKey = $"{_options.Value.CachePrefix}.StatePropertyGetter.{state.GetType().FullName}";

            var getter = _cache.GetOrCreate<Delegate>(cacheKey, x => {
                _logger.Debug($"Generating state property getter delegate for <{state.GetType().GetDisplayName()}>");
                x.SlidingExpiration = _options.Value.BackgroundJobStateDelegateExpiryTime;

                // Generate expression that gets the values of all public readable properties

                // Input
                var stateParameter = Expression.Parameter(state.GetType(), "s");
                var dictionaryParameter = Expression.Parameter(typeof(IDictionary<string, object>), "d");

                // Create block expression where each property is added to dictionary
                var bodyExpressions = new List<Expression>();
                var method = Helper.Expression.GetMethod<IDictionary<string, object>>(x => x.Add(null, null));
                var commonProperties = typeof(IBackgroundJobState).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();
                foreach (var property in state.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanRead && x.GetIndexParameters().Length == 0 && !commonProperties.Select(x => x.Name).Contains(x.Name)))
                {
                    var memberExpression = property.PropertyType.IsValueType ? Expression.Convert(Expression.Property(stateParameter, property), typeof(System.Object)).CastTo<Expression>() : Expression.Property(stateParameter, property);
                    var dictionaryAddExpression = Expression.Call(dictionaryParameter, method, Expression.Constant(property.Name), memberExpression);
                    bodyExpressions.Add(dictionaryAddExpression);
                }
                var body = Expression.Block(bodyExpressions);

                // Create lambda
                var lambda = Expression.Lambda(body, stateParameter, dictionaryParameter);

                _logger.Debug($"Generated state property getter delegate for <{state.GetType().GetDisplayName()}>: {lambda}");

                return lambda.Compile();
            });

            var dictionary = new Dictionary<string, object>();
            _ = getter.Invoke<object>(state, dictionary);

            return dictionary.Select(x => new StorageProperty(x.Key, x.Value));
        }

        private async Task RunTransaction(IStorageConnection connection, AsyncAction action, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            if(!connection.HasTransaction)
            {
                await connection.BeginTransactionAsync(token).ConfigureAwait(false);

                await action().ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
            }
            else
            {
                await action().ConfigureAwait(false);
            }
        }

        private async Task<T> RunTransaction<T>(IStorageConnection connection, AsyncFunc<T> action, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            if (!connection.HasTransaction)
            {
                await connection.BeginTransactionAsync(token).ConfigureAwait(false);

                var result = await action().ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
                return result;
            }
            else
            {
                return await action().ConfigureAwait(false);
            }
        }
    }
}
