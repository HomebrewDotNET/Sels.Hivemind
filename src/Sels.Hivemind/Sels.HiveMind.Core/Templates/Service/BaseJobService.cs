using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using static Sels.Core.Delegates.Async;
using System.Threading.Tasks;
using System.Threading;
using Expression = System.Linq.Expressions.Expression;
using Sels.HiveMind.Query.Job;

namespace Sels.HiveMind.Templates.Service
{
    /// <summary>
    /// base class for creating job services.
    /// </summary>
    public abstract class BaseJobService
    {
        // Fields
        /// <summary>
        /// Used to access the HiveMind options for each environment.
        /// </summary>
        protected readonly IOptionsSnapshot<HiveMindOptions> _options;
        /// <summary>
        /// Used to cache generated expressions.
        /// </summary>
        protected readonly IMemoryCache _cache;
        /// <summary>
        /// Optional logger for tracing.
        /// </summary>
        protected readonly ILogger _logger;

        /// <inheritdoc cref="BaseJobService"/>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        /// <param name="cache"><inheritdoc cref="_cache"/></param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        protected BaseJobService(IOptionsSnapshot<HiveMindOptions> options, IMemoryCache cache, ILogger logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache.ValidateArgument(nameof(cache));
            _logger = logger;
        }

        /// <summary>
        /// Returns a delegate that can converts a <see cref="JobStateStorageData"/> to a <typeparamref name="TState"/>.
        /// </summary>
        /// <typeparam name="TState">The expected state interface</typeparam>
        /// <param name="stateData">Storage data containing the state state</param>
        /// <param name="environment">The HiveMind environment to generate the delegate for</param>
        /// <returns>A generated delegate that can be used to convert <paramref name="stateData"/> to a new instance of <typeparamref name="TState"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        protected Func<IReadOnlyDictionary<string, object>, TState> GetStateConverter<TState>(JobStateStorageData stateData, string environment) where TState : IJobState
        {
            stateData.ValidateArgument(nameof(stateData));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            var type = Type.GetType(stateData.OriginalTypeName);
            var options = _options.Get(environment);

            var cacheKey = $"{options.CachePrefix}.{environment}.StateConstructor.{type.FullName}";

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
                var getOrDefaultMethod = Helper.Expressions.Method.GetMethod(() => Core.Extensions.Collections.CollectionExtensions.GetOrDefault<string>((IReadOnlyDictionary<string, object>)null, null, null)).GetGenericMethodDefinition();
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

                var lambda = Expression.Lambda<Func<IReadOnlyDictionary<string, object>, TState>>(blockExpression, dictionaryParameter);
                _logger.Debug($"Generated state constructor delegate for <{type.GetDisplayName()}>: {lambda}");
                return lambda.Compile();
            });

            return constructor;
        }

        /// <summary>
        /// Generates a delegate that fetches all public properties from <paramref name="jobState"/>.
        /// </summary>
        /// <typeparam name="TState">The type of job state</typeparam>
        /// <param name="jobState">The instance to generate the delegate for</param>
        /// <param name="environment">The HiveMind environment to generate the delegate for</param>
        /// <returns>Delegate that adds the values of all public properties from the supplied state to the supplied dictionary</returns>
        protected Action<TState, IDictionary<string, object>> GetPropertyFetcher<TState>(TState jobState, string environment) where TState : IJobState
        {
            jobState.ValidateArgument(nameof(jobState));
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));

            var options = _options.Get(environment);
            var cacheKey = $"{options.CachePrefix}.{environment}.StatePropertyGetter.{jobState.GetType().FullName}";

            var getter = _cache.GetOrCreate(cacheKey, x =>
            {
                _logger.Debug($"Generating state property getter delegate for <{jobState.GetType().GetDisplayName()}>");
                x.SlidingExpiration = options.DelegateExpiryTime;

                // Generate expression that gets the values of all public readable properties

                // Input
                var stateParameter = Expression.Parameter(typeof(TState), "s");
                var stateVariable = Expression.Variable(jobState.GetType(), "state");
                var castVariable = Expression.Assign(stateVariable, Expression.Convert(stateParameter, jobState.GetType()));
                var dictionaryParameter = Expression.Parameter(typeof(IDictionary<string, object>), "d");

                // Create block expression where each property is added to dictionary
                var bodyExpressions = new List<Expression>();
                var method = Helper.Expressions.Method.GetMethod<IDictionary<string, object>>(x => x.Add(null, null));
                var commonProperties = typeof(TState).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();
                foreach (var property in jobState.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanRead && x.GetIndexParameters().Length == 0 && !commonProperties.Select(x => x.Name).Contains(x.Name) && !x.IsIgnoredStateProperty()))
                {
                    var memberExpression = property.PropertyType.IsValueType ? Expression.Convert(Expression.Property(stateVariable, property), typeof(object)).CastTo<Expression>() : Expression.Property(stateVariable, property);
                    var dictionaryAddExpression = Expression.Call(dictionaryParameter, method, Expression.Constant(property.Name), memberExpression);
                    bodyExpressions.Add(dictionaryAddExpression);
                }
                var body = Expression.Block(stateVariable.AsEnumerable(), Helper.Collection.Enumerate(castVariable, bodyExpressions));

                // Create lambda
                var lambda = Expression.Lambda<Action<TState, IDictionary<string, object>>>(body, stateParameter, dictionaryParameter);

                _logger.Debug($"Generated state property getter delegate for <{jobState.GetType().GetDisplayName()}>: {lambda}");

                return lambda.Compile();
            });

            return getter;
        }

        /// <summary>
        /// Prepares <paramref name="queryConditions"/> so it can be passed down to <see cref="IStorage"/>.
        /// </summary>
        /// <param name="queryConditions">The conditions to prepare</param>
        /// <param name="options">The configured options</param>
        protected void Prepare(JobQueryConditions queryConditions, HiveMindOptions options)
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

        private IEnumerable<JobPropertyCondition> GetPropertyConditions(IEnumerable<JobConditionExpression> expressions, HiveMindOptions options)
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

        /// <summary>
        /// Checks if there is still a valid lock on <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The job to check</param>
        /// <param name="environment">The environment <paramref name="job"/> is from</param>
        /// <exception cref="JobLockStaleException"></exception>
        protected void ValidateLock(JobStorageData job, string environment)
        {
            job.ValidateArgument(nameof(job));
            environment.ValidateArgument(nameof(environment));

            // Check if lock is set
            if (job.Lock == null || !job.Lock.LockedBy.HasValue()) throw new JobLockStaleException(job.Id, environment);

            var options = _options.Get(environment);
            // Check if lock is timed out
            if (DateTime.UtcNow >= job.Lock.LockHeartbeatUtc.Add(options.LockTimeout) - options.LockExpirySafetyOffset) throw new JobLockStaleException(job.Id, environment);
        }
        /// <summary>
        /// Makes sure <paramref name="action"/> is executed using a transaction started on <paramref name="connection"/>.
        /// </summary>
        /// <param name="connection">The connection to maintain the transaction on</param>
        /// <param name="action">The action to execute in the transaction scope</param>
        /// <param name="token">Optional token to cancel the request</param>
        protected async Task RunTransaction(IStorageConnection connection, AsyncAction action, CancellationToken token)
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
        /// <summary>
        /// Makes sure <paramref name="action"/> is executed using a transaction started on <paramref name="connection"/>.
        /// </summary>
        /// <param name="connection">The connection to maintain the transaction on</param>
        /// <param name="action">The action to execute in the transaction scope</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The value returned by calling <paramref name="action"/></returns>
        protected async Task<T> RunTransaction<T>(IStorageConnection connection, AsyncFunc<T> action, CancellationToken token)
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
