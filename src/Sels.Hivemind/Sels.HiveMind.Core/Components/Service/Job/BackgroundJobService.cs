using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Text;
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
using static Sels.HiveMind.HiveMindConstants;
using Expression = System.Linq.Expressions.Expression;
using Sels.Core.Conversion.Converters;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Parameters;
using Sels.Core.Extensions.Linq;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using System.Data.Common;

namespace Sels.HiveMind.Service.Job
{
    /// <inheritdoc cref="IBackgroundJobService"/>
    public class BackgroundJobService : IBackgroundJobService
    {
        // Fields
        private readonly IOptionsSnapshot<HiveMindOptions> _options;
        private readonly IMemoryCache _cache;
        private readonly BackgroundJobValidationProfile _backgroundJobValidationProfile;
        private readonly ILogger _logger;

        /// <inheritdoc cref="BackgroundJobService"/>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="backgroundJobValidationProfile">Used to validate background jobs</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobService(IOptionsSnapshot<HiveMindOptions> options, IMemoryCache cache, BackgroundJobValidationProfile backgroundJobValidationProfile, ILogger<BackgroundJobService> logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache.ValidateArgument(nameof(cache));
            _backgroundJobValidationProfile = backgroundJobValidationProfile.ValidateArgument(nameof(backgroundJobValidationProfile));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> StoreAsync(IStorageConnection connection, JobStorageData job, bool releaseLock, CancellationToken token = default)
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
                    _logger.Log($"Updating background job <{job.Id}> in environment <{connection.Environment}>");
                    ValidateLock(job, connection.Environment);
                    var wasUpdated = await connection.Storage.UpdateBackgroundJobAsync(job, connection, releaseLock, token).ConfigureAwait(false);
                    if (!wasUpdated) throw new BackgroundJobLockStaleException(job.Id, connection.Environment);
                    _logger.Log($"Updated background job <{job.Id}> in environment <{connection.Environment}>");
                    return job.Id;
                }
                // Create
                else
                {
                    _logger.Log($"Creating new background job in environment <{connection.Environment}>");
                    var id = await connection.Storage.CreateBackgroundJobAsync(job, connection, token).ConfigureAwait(false);
                    _logger.Log($"Created background job <{id}> in environment <{connection.Environment}>");
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
            var options = _options.Get(job.Environment);
            var jobStorage = new JobStorageData(job, options, _cache);
            if (job.StateHistory.HasValue())
            {
                foreach (var state in job.StateHistory)
                {
                    var historyProperties = GetStorageProperties(state, options);
                    var stateHistoryStorage = new JobStateStorageData(state, historyProperties);
                    jobStorage.AddState(stateHistoryStorage, job.ChangeTracker.NewStates.Contains(state));
                }
            }
            var properties = GetStorageProperties(job.State, options);
            var stateStorage = new JobStateStorageData(job.State, properties);
            jobStorage.AddState(stateStorage, job.ChangeTracker.NewStates.Contains(job.State));

            return jobStorage;
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> LockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to acquire lock on background job <{id}> in environment <{connection.Environment}> for requester <{requester}>");

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryLockBackgroundJobAsync(id, requester, connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Background job <{id}> does not exist in environment <{connection.Environment}>");
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            if (requester.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Background job <{id}> in environment <{connection.Environment}> is now locked by <{requester}>");
                return jobLock;
            }
            else
            {
                _logger.Warning($"Background job <{id}> in environment <{connection.Environment}> could not be locked by <{requester}> because it is already locked by <{jobLock.LockedBy}>");
                throw new BackgroundJobAlreadyLockedException(id, connection.Environment, requester, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Setting lock heartbeat on background job <{id}> in environment <{connection.Environment}> for <{holder}>");

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryHeartbeatLockAsync(id, holder, connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Background job <{id}> does not exist in environment <{connection.Environment}>");
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            if (holder.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Lock heartbeat on job <{id}> in environment <{connection.Environment}> has been set to <{jobLock.LockHeartbeatUtc}> for <{holder}>");
                return jobLock;
            }
            else
            {
                _logger.Warning($"Lock heartbeat on job <{id}> in environment <{connection.Environment}> could not be set for <{holder}> because it is already locked by <{jobLock.LockedBy}>");
                throw new BackgroundJobAlreadyLockedException(id, connection.Environment, holder, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task<JobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{id}> from environment <{connection.Environment}>");

            var job = await connection.Storage.GetBackgroundJobAsync(id, connection, token).ConfigureAwait(false);

            if (job == null)
            {
                _logger.Warning($"Background job <{id}> does not exist in environment <{connection.Environment}>");
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            _logger.Log($"Fetched job <{id}> from environment <{connection.Environment}>");
            return job;
        }
        /// <inheritdoc/>
        public IBackgroundJobState ConvertToState(JobStateStorageData stateData, HiveMindOptions options)
        {
            stateData.ValidateArgument(nameof(stateData));
            options.ValidateArgument(nameof(options));

            var type = Type.GetType(stateData.OriginalType);

            var cacheKey = $"{options.CachePrefix}.StateConstructor.{type.FullName}";

            var constructor = _cache.GetOrCreate<Func<IReadOnlyDictionary<string, object>, IBackgroundJobState>>(cacheKey, x =>
            {
                _logger.Debug($"Generating state constructor delegate for <{type.GetDisplayName()}>");
                x.SlidingExpiration = options.BackgroundJobStateDelegateExpiryTime;

                var stateProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetIndexParameters().Length == 0 && !x.IsIgnoredStateProperty()).ToArray();
                var stateSettableProperties = stateProperties.Where(x => x.CanWrite).ToArray();
                var constructors = type.GetConstructors(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).OrderByDescending(x => x.GetParameters().Length);

                // Generate expression that creates a new instance for the state and sets the properties.
                ConstructorInfo constructor = null;
                // Determine the constructor to use. Prefer constructors where all parameters match settable properties.
                constructor = constructors.FirstOrDefault(x => {
                    bool matched = true;

                    foreach(var parameter in x.GetParameters())
                    {
                        if(!stateProperties.Select(x => x.Name).Contains(parameter.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            _logger.Debug($"Constructor parameter <{parameter.Name}> does not match any property on <{type.GetDisplayName()}>. Skipping");
                            matched = false;
                            break;
                        }
                    }

                    return matched;
                });
                if (constructor == null) throw new InvalidOperationException($"Could not find any constructor to use for state <{type.GetDisplayName()}>. Make sure at least 1 constructor is public with either no parameters or where all parameters match the names of properties");

                // Input
                var dictionaryParameter = Expression.Parameter(typeof(IReadOnlyDictionary<string, object>), "d");

                // Create expression that creates instance
                List<Expression> arguments = new List<System.Linq.Expressions.Expression>();
                var getOrDefaultMethod = Helper.Expression.GetMethod(() => CollectionExtensions.GetValueOrDefault<string, object>(null, null));
                var convertToOrDefaultMethod = Helper.Expression.GetMethod<ITypeConverter>(x => Core.Conversion.Extensions.ConversionExtensions.ConvertToOrDefault<string>(null, (IReadOnlyDictionary<string, object>)null));
                foreach(var parameter in constructor.GetParameters())
                {
                    var getValueExpression = Expression.Call(getOrDefaultMethod, dictionaryParameter, Expression.Constant(parameter.Name));
                    var convertExpression = Expression.Convert(getValueExpression, parameter.ParameterType);
                    arguments.Add(convertExpression);
                }
                var constructorExpression = Expression.New(constructor, arguments.ToArray());
                var variableExpression = Expression.Variable(type, "instance");
                var variableAssignmentExpression = Expression.Assign(variableExpression, constructorExpression);

                // Create expression that set the properties on the state
                List<Expression> setters = new List<System.Linq.Expressions.Expression>();
                foreach(var property in stateSettableProperties)
                {
                    var getValueExpression = Expression.Call(getOrDefaultMethod, dictionaryParameter, Expression.Constant(property.Name));
                    var convertExpression = Expression.Convert(getValueExpression, property.PropertyType);
                    
                    var propertyExpresion = Expression.Property(variableExpression, property.Name);
                    var assignmentExpression = Expression.Assign(propertyExpresion, convertExpression);
                    setters.Add(assignmentExpression);
                }

                // Generate delegate
                var blockExpression = Expression.Block(variableExpression.AsEnumerable(), Helper.Collection.EnumerateAll(variableAssignmentExpression.AsEnumerable(), setters, variableExpression.AsEnumerable()));

                var lambda = Expression.Lambda<Func<IReadOnlyDictionary<string, object>, IBackgroundJobState>>(blockExpression, dictionaryParameter);
                _logger.Debug($"Generated state constructor delegate for <{type.GetDisplayName()}>: {lambda}");
                return lambda.Compile();
            });

            var dictionary = new Dictionary<string, object>
            {
                { nameof(IBackgroundJobState.ElectedDateUtc), stateData.ElectedDateUtc },
                { nameof(IBackgroundJobState.Reason), stateData.Reason },
                { nameof(IBackgroundJobState.Name), stateData.Name }
            };


            stateData.Properties.Execute(x => dictionary.Add(x.Name, x.GetValue(options, _cache)));

            return constructor(dictionary);
        }
        /// <summary>
        /// Gets all the properties to store for <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to get the properties for</param>
        /// <param name="options">The options for caching</param>
        /// <returns>All the state properties to store for <paramref name="state"/> if there are any</returns>
        protected virtual IEnumerable<StorageProperty> GetStorageProperties(IBackgroundJobState state, HiveMindOptions options)
        {
            state.ValidateArgument(nameof(state));
            options.ValidateArgument(nameof(options));

            var cacheKey = $"{options.CachePrefix}.StatePropertyGetter.{state.GetType().FullName}";

            var getter = _cache.GetOrCreate(cacheKey, x =>
            {
                _logger.Debug($"Generating state property getter delegate for <{state.GetType().GetDisplayName()}>");
                x.SlidingExpiration = options.BackgroundJobStateDelegateExpiryTime;

                // Generate expression that gets the values of all public readable properties

                // Input
                var stateParameter = Expression.Parameter(typeof(object), "s");
                var stateVariable = Expression.Variable(state.GetType(), "state");
                var castVariable = Expression.Assign(stateVariable, Expression.Convert(stateParameter, state.GetType()));
                var dictionaryParameter = Expression.Parameter(typeof(IDictionary<string, object>), "d");

                // Create block expression where each property is added to dictionary
                var bodyExpressions = new List<Expression>();
                var method = Helper.Expression.GetMethod<IDictionary<string, object>>(x => x.Add(null, null));
                var commonProperties = typeof(IBackgroundJobState).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();
                foreach (var property in state.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanRead && x.GetIndexParameters().Length == 0 && !commonProperties.Select(x => x.Name).Contains(x.Name) && !x.IsIgnoredStateProperty()))
                {
                    var memberExpression = property.PropertyType.IsValueType ? Expression.Convert(Expression.Property(stateVariable, property), typeof(System.Object)).CastTo<Expression>() : Expression.Property(stateVariable, property);
                    var dictionaryAddExpression = Expression.Call(dictionaryParameter, method, Expression.Constant(property.Name), memberExpression);
                    bodyExpressions.Add(dictionaryAddExpression);
                }
                var body = Expression.Block(stateVariable.AsEnumerable(), Helper.Collection.Enumerate(castVariable, bodyExpressions));

                // Create lambda
                var lambda = Expression.Lambda<Action<object, IDictionary<string, object>>>(body, stateParameter, dictionaryParameter);

                _logger.Debug($"Generated state property getter delegate for <{state.GetType().GetDisplayName()}>: {lambda}");

                return lambda.Compile();
            });

            var dictionary = new Dictionary<string, object>();
            getter(state, dictionary);

            return dictionary.Select(x => new StorageProperty(x.Key, x.Value, options, _cache));
        }

        private void ValidateLock(JobStorageData job, string environment)
        {
            job.ValidateArgument(nameof(job));
            environment.ValidateArgument(nameof(environment));

            // Check if lock is set
            if (job.Lock == null || !job.Lock.LockedBy.HasValue()) throw new BackgroundJobLockStaleException(job.Id, environment);

            // Check if lock is timed out
            if(DateTime.UtcNow >= (job.Lock.LockedAtUtc.Add(_options.Get(environment).LockTimeout))) throw new BackgroundJobLockStaleException(job.Id, environment);
        }
        private async Task RunTransaction(IStorageConnection connection, AsyncAction action, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            if (!connection.HasTransaction)
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
