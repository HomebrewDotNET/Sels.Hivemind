using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for selecting what to place a condition on when querying background jobs.
    /// </summary>
    public interface IQueryBackgroundJobConditionBuilder
    {
        /// <summary>
        /// Groups together multiple conditions.
        /// </summary>
        /// <param name="builder">Builder for defining the conditions</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> Group(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> builder);
        /// <summary>
        /// Adds a condition on the queue of a background job.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string> Queue { get; }
        /// <summary>
        /// Adds a condition on the priority of a background job.
        /// </summary>
        IQueryConditionComparisonBuilder<QueuePriority> Priority { get; }
        /// <summary>
        /// Adds a condition on the creation date of a background job.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime> CreatedAt { get; }
        /// <summary>
        /// Adds a condition on the last modification date of a background job.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime> ModifiedAt { get; }
        /// <summary>
        /// Adds a condition on the current state of a background job.
        /// </summary>
        IQueryBackgroundJobStateConditionBuilder CurrentState { get; }
        /// <summary>
        /// Adds a condition on a past state of a background job.
        /// </summary>
        IQueryBackgroundJobStateConditionBuilder PastState { get; }
        /// <summary>
        /// Adds a condition the holder of a lock on a background job.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string> LockedBy { get; }
        /// <summary>
        /// Adds a condition on the property of a background job.
        /// </summary>
        /// <param name="name">The name of the background job property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryBackgroundJobPropertyConditionBuilder Property(string name);
    }
    /// <summary>
    /// Builder for defining conditions on a state of a background job.
    /// </summary>
    public interface IQueryBackgroundJobStateConditionBuilder
    {
        /// <summary>
        /// Adds a query condition on the state name of a background job.
        /// </summary>
        /// <returns>Builder for defining more conditions</returns>
        IQueryConditionTextComparisonBuilder<string> Name { get; }
        /// <summary>
        /// Adds a query condition on the state transition reason of a background job.
        /// </summary>
        /// <returns>Builder for defining more conditions</returns>
        IQueryConditionTextComparisonBuilder<string> Reason { get; }
        /// <summary>
        /// Adds a query condition on the elected date on the state of the background job.
        /// </summary>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime> ElectedDate { get; }
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="name">The name of the state property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryBackgroundJobPropertyConditionBuilder Property(string name);

        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<string> Property<TState>(Expression<Func<TState, string>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsString;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<Guid?> Property<TState>(Expression<Func<TState, Guid?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsGuid;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<Guid?> Property<TState>(Expression<Func<TState, Guid>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsGuid;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<TimeSpan?> Property<TState>(Expression<Func<TState, TimeSpan>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsTimespan;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<TimeSpan?> Property<TState>(Expression<Func<TState, TimeSpan?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsTimespan;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T?> Property<TState, T>(Expression<Func<TState, T?>> propertyExpression) where T : struct, Enum => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsEnum<T>();
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T?> Property<TState, T>(Expression<Func<TState, T>> propertyExpression) where T : struct, Enum => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsEnum<T>();
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<short?> Property<TState>(Expression<Func<TState, short?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsShort;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<short?> Property<TState>(Expression<Func<TState, short>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsShort;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<int?> Property<TState>(Expression<Func<TState, int>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsInt;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<int?> Property<TState>(Expression<Func<TState, int?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsInt;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<long?> Property<TState>(Expression<Func<TState, long>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsLong;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<long?> Property<TState>(Expression<Func<TState, long?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsLong;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<byte?> Property<TState>(Expression<Func<TState, byte>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsByte;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<byte?> Property<TState>(Expression<Func<TState, byte?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsByte;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<bool?> Property<TState>(Expression<Func<TState, bool>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsBool;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<bool?> Property<TState>(Expression<Func<TState, bool?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsBool;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<decimal?> Property<TState>(Expression<Func<TState, decimal>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDecimal;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<decimal?> Property<TState>(Expression<Func<TState, decimal?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDecimal;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<float?> Property<TState>(Expression<Func<TState, float>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsFloat;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<float?> Property<TState>(Expression<Func<TState, float?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsFloat;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<double?> Property<TState>(Expression<Func<TState, double>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDouble;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<double?> Property<TState>(Expression<Func<TState, double?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDouble;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime?> Property<TState>(Expression<Func<TState, DateTime>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDate;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime?> Property<TState>(Expression<Func<TState, DateTime?>> propertyExpression) => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDate;
    }

    /// <summary>
    /// Builder for defining conditions on a (background job or state) property.
    /// </summary>
    public interface IQueryBackgroundJobPropertyConditionBuilder
    {
        /// <summary>
        /// Queries a property of type <see cref="string"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string> AsString { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<Guid?> AsGuid { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<TimeSpan?> AsTimespan { get; }
        /// <summary>
        /// Queries a property of type <see cref="Enum"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<T?> AsEnum<T>() where T : struct, Enum;
        /// <summary>
        /// Queries a property of type <see cref="short"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<short?> AsShort { get; }
        /// <summary>
        /// Queries a property of type <see cref="int"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<int?> AsInt { get; }
        /// <summary>
        /// Queries a property of type <see cref="long"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<long?> AsLong { get; }
        /// <summary>
        /// Queries a property of type <see cref="byte"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<byte?> AsByte { get; }
        /// <summary>
        /// Queries a property of type <see cref="bool"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<bool?> AsBool { get; }
        /// <summary>
        /// Queries a property of type <see cref="decimal"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<decimal?> AsDecimal { get; }
        /// <summary>
        /// Queries a property of type <see cref="float"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<float?> AsFloat { get; }
        /// <summary>
        /// Queries a property of type <see cref="double"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<double?> AsDouble { get; }
        /// <summary>
        /// Queries a property of type <see cref="DateTime"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime?> AsDate { get; }
    }
}
