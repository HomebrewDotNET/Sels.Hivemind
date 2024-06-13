using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Job;
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
    /// Builder for selecting what to place a condition on when querying jobs.
    /// </summary>
    public interface IQueryJobConditionBuilder
    {
        /// <summary>
        /// Groups together multiple conditions.
        /// </summary>
        /// <param name="builder">Builder for defining the conditions</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> Group(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> builder);
        /// <summary>
        /// Adds a condition on the queue of a background job.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> Queue { get; }
        /// <summary>
        /// Adds a condition on the creation date of a background job.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> CreatedAt { get; }
        /// <summary>
        /// Adds a condition on the last modification date of a background job.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> ModifiedAt { get; }
        /// <summary>
        /// Adds a condition on the current state of a background job.
        /// </summary>
        IQueryJobStateConditionBuilder CurrentState { get; }
        /// <summary>
        /// Adds a condition on a past state of a background job.
        /// </summary>
        IQueryJobStateConditionBuilder PastState { get; }
        /// <summary>
        /// Adds a condition on the property of a background job.
        /// </summary>
        /// <param name="name">The name of the background job property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryJobPropertyConditionBuilder Property(string name);
    }
    /// <summary>
    /// Builder for defining conditions on a state of a job.
    /// </summary>
    public interface IQueryJobStateConditionBuilder
    {
        /// <summary>
        /// Adds a query condition on the state name of a job.
        /// </summary>
        /// <returns>Builder for defining more conditions</returns>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> Name { get; }
        /// <summary>
        /// Adds a query condition on the elected date on the state of the background job.
        /// </summary>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> ElectedDate { get; }
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="name">The name of the state property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryJobPropertyConditionBuilder Property(string name);

        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, string>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsString;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<Guid?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, Guid?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsGuid;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<Guid?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, Guid>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsGuid;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<TimeSpan?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, TimeSpan>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsTimespan;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<TimeSpan?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, TimeSpan?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsTimespan;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T?, IQueryJobConditionBuilder> Property<TState, T>(Expression<Func<TState, T?>> propertyExpression) where TState : IJobState where T : struct, Enum  => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsEnum<T>();
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T?, IQueryJobConditionBuilder> Property<TState, T>(Expression<Func<TState, T>> propertyExpression) where TState : IJobState where T : struct, Enum => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsEnum<T>();
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<short?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, short?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsShort;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<short?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, short>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsShort;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<int?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, int>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsInt;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<int?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, int?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsInt;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<long?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, long>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsLong;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<long?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, long?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsLong;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<byte?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, byte>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsByte;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<byte?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, byte?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsByte;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<bool?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, bool>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsBool;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<bool?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, bool?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsBool;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<decimal?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, decimal>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDecimal;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<decimal?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, decimal?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDecimal;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<float?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, float>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsFloat;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<float?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, float?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsFloat;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<double?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, double>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDouble;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<double?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, double?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDouble;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, DateTime>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDate;
        /// <summary>
        /// Adds a condition on the property of a state of a background job.
        /// </summary>
        /// <param name="propertyExpression">Expression that points to the property to query</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime?, IQueryJobConditionBuilder> Property<TState>(Expression<Func<TState, DateTime?>> propertyExpression) where TState : IJobState => Property(propertyExpression.ExtractProperty(nameof(propertyExpression)).Name).AsDate;
    }

    /// <summary>
    /// Builder for defining conditions on a (job or state) property.
    /// </summary>
    public interface IQueryJobPropertyConditionBuilder
    {
        /// <summary>
        /// Queries a property of type <see cref="string"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> AsString { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<Guid?, IQueryJobConditionBuilder> AsGuid { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<TimeSpan?, IQueryJobConditionBuilder> AsTimespan { get; }
        /// <summary>
        /// Queries a property of type <see cref="Enum"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<T?, IQueryJobConditionBuilder> AsEnum<T>() where T : struct, Enum;
        /// <summary>
        /// Queries a property of type <see cref="short"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<short?, IQueryJobConditionBuilder> AsShort { get; }
        /// <summary>
        /// Queries a property of type <see cref="int"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<int?, IQueryJobConditionBuilder> AsInt { get; }
        /// <summary>
        /// Queries a property of type <see cref="long"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<long?, IQueryJobConditionBuilder> AsLong { get; }
        /// <summary>
        /// Queries a property of type <see cref="byte"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<byte?, IQueryJobConditionBuilder> AsByte { get; }
        /// <summary>
        /// Queries a property of type <see cref="bool"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<bool?, IQueryJobConditionBuilder> AsBool { get; }
        /// <summary>
        /// Queries a property of type <see cref="decimal"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<decimal?, IQueryJobConditionBuilder> AsDecimal { get; }
        /// <summary>
        /// Queries a property of type <see cref="float"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<float?, IQueryJobConditionBuilder> AsFloat { get; }
        /// <summary>
        /// Queries a property of type <see cref="double"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<double?, IQueryJobConditionBuilder> AsDouble { get; }
        /// <summary>
        /// Queries a property of type <see cref="DateTime"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime?, IQueryJobConditionBuilder> AsDate { get; }
    }
}
