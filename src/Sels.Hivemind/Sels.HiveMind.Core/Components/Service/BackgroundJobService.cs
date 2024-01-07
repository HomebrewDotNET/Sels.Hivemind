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
using Sels.HiveMind;
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
using System.Data.Common;
using Sels.HiveMind.Query.Job;
using static Sels.Core.Delegates;

namespace Sels.HiveMind.Service
{
    /// <inheritdoc cref="IBackgroundJobService"/>
    public class BackgroundJobService : IBackgroundJobService
    {
        // Fields
        private readonly IOptionsSnapshot<HiveMindOptions> _options;
        private readonly IMemoryCache _cache;
        private readonly BackgroundJobValidationProfile _backgroundJobValidationProfile;
        private readonly BackgroundJobQueryValidationProfile _backgroundJobQueryValidationProfile;
        private readonly ILogger _logger;

        /// <inheritdoc cref="BackgroundJobService"/>
        /// <param name="options">Used to access the HiveMind options for each environment</param>
        /// <param name="backgroundJobValidationProfile">Used to validate background jobs</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobService(IOptionsSnapshot<HiveMindOptions> options, IMemoryCache cache, BackgroundJobValidationProfile backgroundJobValidationProfile, BackgroundJobQueryValidationProfile backgroundJobQueryValidationProfile, ILogger<BackgroundJobService> logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache.ValidateArgument(nameof(cache));
            _backgroundJobValidationProfile = backgroundJobValidationProfile.ValidateArgument(nameof(backgroundJobValidationProfile));
            _backgroundJobQueryValidationProfile = backgroundJobQueryValidationProfile.ValidateArgument(nameof(backgroundJobQueryValidationProfile));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> StoreAsync(IStorageConnection connection, JobStorageData job, bool releaseLock, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            job.ValidateArgument(nameof(job));

            // Validate storage data
            var result = await _backgroundJobValidationProfile.ValidateAsync(job, null).ConfigureAwait(false);
            if (!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(job);

            return await RunTransaction(connection, async () =>
            {
                // Update
                if (job.Id.HasValue())
                {
                    _logger.Log($"Updating background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, connection.Environment);
                    ValidateLock(job, connection.Environment);
                    var wasUpdated = await connection.Storage.TryUpdateBackgroundJobAsync(job, connection, releaseLock, token).ConfigureAwait(false);
                    if (!wasUpdated) throw new BackgroundJobLockStaleException(job.Id, connection.Environment);
                    _logger.Log($"Updated background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", job.Id, connection.Environment);
                    return job.Id;
                }
                // Create
                else
                {
                    _logger.Log($"Creating new background job in environment <{HiveLog.Environment}>");
                    var id = await connection.Storage.CreateBackgroundJobAsync(job, connection, token).ConfigureAwait(false);
                    _logger.Log($"Created background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
                    return id;
                }
            }, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> LockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to acquire lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for requester <{requester}>", id, connection.Environment);

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryLockBackgroundJobAsync(id, requester, connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Background job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            if (requester.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, requester);
                return jobLock;
            }
            else
            {
                _logger.Warning($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> could not be locked by <{requester}> because it is already locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);
                throw new BackgroundJobAlreadyLockedException(id, connection.Environment, requester, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task<bool> TryLockAsync(string id, IStorageConnection connection, string requester = null, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to acquire lock on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for requester <{requester}>", id, connection.Environment);

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryLockBackgroundJobAsync(id, requester, connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Background job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            if (requester.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is now locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, requester);
                return true;
            }
            else
            {
                _logger.Warning($"Background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> could not be locked by <{requester}> because it is already locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);
                return false;
            }
        }
        /// <inheritdoc/>
        public async Task<LockStorageData> HeartbeatLockAsync(string id, string holder, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            holder.ValidateArgumentNotNullOrWhitespace(nameof(holder));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Setting lock heartbeat on background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for <{holder}>", id, connection.Environment);

            var jobLock = await RunTransaction(connection, async () =>
            {
                return await connection.Storage.TryHeartbeatLockAsync(id, holder, connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (jobLock == null)
            {
                _logger.Warning($"Background job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            if (holder.EqualsNoCase(jobLock.LockedBy))
            {
                _logger.Log($"Lock heartbeat on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> has been set to <{jobLock.LockHeartbeatUtc}> for <{HiveLog.Job.LockHolder}>", id, connection.Environment, holder);
                return jobLock;
            }
            else
            {
                _logger.Warning($"Lock heartbeat on job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> could not be set for <{holder}> because it is already locked by <{HiveLog.Job.LockHolder}>", id, connection.Environment, jobLock.LockedBy);
                throw new BackgroundJobAlreadyLockedException(id, connection.Environment, holder, jobLock.LockedBy);
            }
        }
        /// <inheritdoc/>
        public async Task<JobStorageData> GetAsync(string id, IStorageConnection connection, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));

            _logger.Log($"Fetching job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);

            var job = await connection.Storage.GetBackgroundJobAsync(id, connection, token).ConfigureAwait(false);

            if (job == null)
            {
                _logger.Warning($"Background job <{HiveLog.Job.Id}> does not exist in environment <{HiveLog.Environment}>", id, connection.Environment);
                throw new BackgroundJobNotFoundException(id, connection.Environment);
            }

            _logger.Log($"Fetched job <{HiveLog.Job.Id}> from environment <{HiveLog.Environment}>", id, connection.Environment);
            return job;
        }
        /// <inheritdoc/>
        public async Task<(JobStorageData[] Results, long Total)> SearchAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int pageSize, int page, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));
            page.ValidateArgumentLargerOrEqual(nameof(page), 1);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 1);
            pageSize.ValidateArgumentSmallerOrEqual(nameof(pageSize), HiveMindConstants.Query.MaxResultLimit);

            _logger.Log($"Searching for background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.SearchBackgroundJobsAsync(connection, queryConditions, pageSize, page, orderBy, orderByDescending, token), token).ConfigureAwait(false);

            _logger.Log($"Search for background jobs in environment <{HiveLog.Environment}> returned <{result.Results.Length}> jobs out of the total <{result.Total}> matching", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task<long> CountAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));

            _logger.Log($"Searching for an amount of background jobs in environment <{HiveLog.Environment}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.CountBackgroundJobsAsync(connection, queryConditions, token), token).ConfigureAwait(false);

            _logger.Log($"Search for an amount of background jobs in environment <{HiveLog.Environment}> returned <{result}> matching", connection.Environment);
            return result;
        }
        /// <inheritdoc/>
        public async Task<(JobStorageData[] Results, long Total)> LockAsync(IStorageConnection connection, BackgroundJobQueryConditions queryConditions, int limit, string requester, bool allowAlreadyLocked, QueryBackgroundJobOrderByTarget? orderBy, bool orderByDescending = false, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            queryConditions.ValidateArgument(nameof(queryConditions));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);
            requester ??= Guid.NewGuid().ToString();
            requester.ValidateArgumentNotNullOrWhitespace(nameof(requester));

            _logger.Log($"Trying to lock the next <{limit}> background jobs in environment <{HiveLog.Environment}> for <{requester}>", connection.Environment);

            // Validate query parameters
            var validationResult = await _backgroundJobQueryValidationProfile.ValidateAsync(queryConditions, null).ConfigureAwait(false);
            if (!validationResult.IsValid) validationResult.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(queryConditions);

            // Convert properties to storage format
            Prepare(queryConditions, _options.Get(connection.Environment));

            // Query storage
            var result = await RunTransaction(connection, () => connection.Storage.LockBackgroundJobsAsync(connection, queryConditions, limit, requester, allowAlreadyLocked, orderBy, orderByDescending, token), token).ConfigureAwait(false);

            _logger.Log($"<{result.Results.Length}> background jobs in environment <{HiveLog.Environment}> are now locked by <{HiveLog.Job.LockHolder}> out of the total <{result.Total}> matching", connection.Environment, requester);
            return result;
        }
        /// <inheritdoc/>
        public async Task<(bool Exists, T Data)> TryGetDataAsync<T>(IStorageConnection connection, string id, string name, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgument(nameof(name));

            _logger.Log($"Trying to fetch data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);

            if (await connection.Storage.TryGetBackgroundJobDataAsync(connection, id, name, token).ConfigureAwait(false) is (true, var data))
            {
                _logger.Debug($"Fetched data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>. Converting to <{typeof(T)}>", id, connection.Environment);
                var converted = HiveMindHelper.Storage.ConvertFromStorageFormat(data, typeof(T), _options.Get(connection.Environment), _cache).CastTo<T>();
                _logger.Log($"Fetched data <{name}> from background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
                return (true, converted);
            }
            return (false, default);
        }
        /// <inheritdoc/>
        public async Task SetDataAsync<T>(IStorageConnection connection, string id, string name, T value, CancellationToken token = default)
        {
            id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            connection.ValidateArgument(nameof(connection));
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            _logger.Log($"Saving data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
            _logger.Debug($"Converting data <{name}> of type <{value.GetType()}> for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> for storage", id, connection.Environment);

            var converted = HiveMindHelper.Storage.ConvertToStorageFormat(value, _options.Get(connection.Environment), _cache);

            await RunTransaction(connection, () => connection.Storage.SetBackgroundJobDataAsync(connection, id, name, converted, token), token).ConfigureAwait(false);

            _logger.Log($"Saved data <{name}> to background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", id, connection.Environment);
        }
        /// <inheritdoc/>
        public async Task<JobStorageData[]> GetTimedOutBackgroundJobs(IStorageConnection connection, int limit, string requester, CancellationToken token = default)
        {
            connection.ValidateArgument(nameof(connection));
            requester.ValidateArgument(nameof(requester));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 1);
            limit.ValidateArgumentSmallerOrEqual(nameof(limit), HiveMindConstants.Query.MaxDequeueLimit);

            _logger.Log($"Checking if there are timed out background jobs in environment <{HiveLog.Environment}> which will be locked for <{requester}>", connection.Environment);

            var options = _options.Get(connection.Environment);

            var jobs = await RunTransaction(connection, () => connection.Storage.GetTimedOutBackgroundJobs(connection, limit, requester, options.LockTimeout, token), token).ConfigureAwait(false);

            if (jobs.HasValue())
            {
                _logger.Log($"Got <{jobs.Length}> timed out background jobs in environment <{HiveLog.Environment}> to lock for <{requester}>", connection.Environment);
                return jobs;
            }
            else
            {
                _logger.Log($"No timed out background jobs in environment <{HiveLog.Environment}> to lock for <{requester}>", connection.Environment);
                return Array.Empty<JobStorageData>();
            }
        }


        private void Prepare(BackgroundJobQueryConditions queryConditions, HiveMindOptions options)
        {
            queryConditions.ValidateArgument(nameof(queryConditions));
            options.ValidateArgument(nameof(options));

            if (queryConditions.Conditions.HasValue())
            {
                foreach (var propertyCondition in GetPropertyConditions(queryConditions.Conditions.Where(x => x.Expression != null).Select(x => x.Expression), options))
                {
                    if (propertyCondition?.Comparison?.Value != null)
                    {
                        propertyCondition.Comparison.Value = HiveMindHelper.Storage.ConvertToStorageFormat(propertyCondition.Type, propertyCondition.Comparison.Value, options, _cache);
                    }
                    else if (propertyCondition?.Comparison?.Values != null)
                    {
                        propertyCondition.Comparison.Values = propertyCondition.Comparison.Values.Select(x => HiveMindHelper.Storage.ConvertToStorageFormat(propertyCondition.Type, x, options, _cache)).ToArray();
                    }
                }
            }
        }

        private IEnumerable<BackgroundJobPropertyCondition> GetPropertyConditions(IEnumerable<BackgroundJobConditionExpression> expressions, HiveMindOptions options)
        {
            expressions.ValidateArgument(nameof(expressions));
            options.ValidateArgument(nameof(options));

            foreach (var expression in expressions)
            {
                if (expression.IsGroup)
                {
                    foreach (var propertyCondition in GetPropertyConditions(expression.Group.Conditions.Where(x => x.Expression != null).Select(x => x.Expression), options))
                    {
                        yield return propertyCondition;
                    }
                }
                else
                {
                    var condition = expression.Condition;

                    if (condition.PropertyComparison != null)
                    {
                        yield return condition.PropertyComparison;
                    }
                    else if (condition.CurrentStateComparison?.PropertyComparison != null)
                    {
                        yield return condition.CurrentStateComparison.PropertyComparison;
                    }
                    else if (condition.PastStateComparison?.PropertyComparison != null)
                    {
                        yield return condition.PastStateComparison.PropertyComparison;
                    }
                    else if (condition.AnyStateComparison?.PropertyComparison != null)
                    {
                        yield return condition.AnyStateComparison.PropertyComparison;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public virtual IBackgroundJobState ConvertToState(JobStateStorageData stateData, HiveMindOptions options)
        {
            stateData.ValidateArgument(nameof(stateData));
            options.ValidateArgument(nameof(options));

            var type = Type.GetType(stateData.OriginalTypeName);

            var cacheKey = $"{options.CachePrefix}.StateConstructor.{type.FullName}";

            var constructor = _cache.GetOrCreate(cacheKey, x =>
            {
                _logger.Debug($"Generating state constructor delegate for <{type.GetDisplayName()}>");
                x.SlidingExpiration = options.DelegateExpiryTime;

                var stateProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetIndexParameters().Length == 0 && !x.IsIgnoredStateProperty()).ToArray();
                var stateSettableProperties = stateProperties.Where(x => x.CanWrite).ToArray();
                var constructors = type.GetConstructors(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).OrderByDescending(x => x.GetParameters().Length);

                // Generate expression that creates a new instance for the state and sets the properties.
                ConstructorInfo constructor = null;
                // Determine the constructor to use. Prefer constructors where all parameters match settable properties.
                constructor = constructors.FirstOrDefault(x =>
                {
                    bool matched = true;

                    foreach (var parameter in x.GetParameters())
                    {
                        if (!stateProperties.Select(x => x.Name).Contains(parameter.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            _logger.Debug($"Constructor parameter <{parameter.Name}> does not match any property on <{type.GetDisplayName()}>. Skipping");
                            matched = false;
                            break;
                        }
                    }

                    return matched;
                });
                if (constructor == null) throw new InvalidOperationException($"Could not find any constructor to use for state <{type.GetDisplayName()}>. Make sure at least 1 constructor is public with either no parameters or where all parameters match the names of public properties");

                // Input
                var dictionaryParameter = Expression.Parameter(typeof(IReadOnlyDictionary<string, object>), "d");

                // Create expression that creates instance
                List<Expression> arguments = new List<Expression>();
                var getOrDefaultMethod = Helper.Expression.GetMethod(() => Core.Extensions.Collections.CollectionExtensions.GetOrDefault<string>((IReadOnlyDictionary<string, object>)null, null, null)).GetGenericMethodDefinition();
                foreach (var parameter in constructor.GetParameters())
                {
                    var getValueExpression = Expression.Call(getOrDefaultMethod.MakeGenericMethod(parameter.ParameterType), dictionaryParameter, Expression.Constant(parameter.Name), Expression.Constant(null, typeof(Func<>).MakeGenericType(parameter.ParameterType)));
                    arguments.Add(getValueExpression);
                }
                var constructorExpression = Expression.New(constructor, arguments.ToArray());
                var variableExpression = Expression.Variable(type, "instance");
                var variableAssignmentExpression = Expression.Assign(variableExpression, constructorExpression);

                // Create expression that set the properties on the state
                List<Expression> setters = new List<Expression>();
                foreach (var property in stateSettableProperties)
                {
                    var getValueExpression = Expression.Call(getOrDefaultMethod.MakeGenericMethod(property.PropertyType), dictionaryParameter, Expression.Constant(property.Name), Expression.Constant(null, typeof(Func<>).MakeGenericType(property.PropertyType)));

                    var propertyExpresion = Expression.Property(variableExpression, property.Name);
                    var assignmentExpression = Expression.Assign(propertyExpresion, getValueExpression);
                    setters.Add(assignmentExpression);
                }

                // Generate delegate
                var blockExpression = Expression.Block(variableExpression.AsEnumerable(), Helper.Collection.EnumerateAll(variableAssignmentExpression.AsEnumerable(), setters, variableExpression.AsEnumerable()));

                var lambda = Expression.Lambda<Func<IReadOnlyDictionary<string, object>, IBackgroundJobState>>(blockExpression, dictionaryParameter);
                _logger.Debug($"Generated state constructor delegate for <{type.GetDisplayName()}>: {lambda}");
                return lambda.Compile();
            });

            var properties = new LazyPropertyInfoDictionary(options, _cache);
            if (stateData.Properties.HasValue()) stateData.Properties.Execute(x => properties.Add(x.Name, new LazyPropertyInfo(x, options, _cache)));
            properties.Add(nameof(IBackgroundJobState.ElectedDateUtc), new LazyPropertyInfo(stateData.ElectedDateUtc, options, _cache));
            properties.Add(nameof(IBackgroundJobState.Reason), new LazyPropertyInfo(stateData.Reason, options, _cache));
            properties.Add(nameof(IBackgroundJobState.Name), new LazyPropertyInfo(stateData.Name, options, _cache));

            return constructor(properties);
        }
        /// <inheritdoc/>
        public virtual IEnumerable<StorageProperty> GetStorageProperties(IBackgroundJobState state, HiveMindOptions options)
        {
            state.ValidateArgument(nameof(state));
            options.ValidateArgument(nameof(options));

            var cacheKey = $"{options.CachePrefix}.StatePropertyGetter.{state.GetType().FullName}";

            var getter = _cache.GetOrCreate(cacheKey, x =>
            {
                _logger.Debug($"Generating state property getter delegate for <{state.GetType().GetDisplayName()}>");
                x.SlidingExpiration = options.DelegateExpiryTime;

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
                    var memberExpression = property.PropertyType.IsValueType ? Expression.Convert(Expression.Property(stateVariable, property), typeof(object)).CastTo<Expression>() : Expression.Property(stateVariable, property);
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

            var options = _options.Get(environment);
            // Check if lock is timed out
            if (DateTime.UtcNow >= job.Lock.LockHeartbeatUtc.Add(options.LockTimeout) - options.LockExpirySafetyOffset) throw new BackgroundJobLockStaleException(job.Id, environment);
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
