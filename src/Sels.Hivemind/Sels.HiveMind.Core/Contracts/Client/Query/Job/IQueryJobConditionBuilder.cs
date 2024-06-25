using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
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
        /// <summary>
        /// Adds a condition on the property of a background job where the expected type of the property is <typeparamref name="T"/>.
        /// </summary>
        /// <param name="name">The name of the background job property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder> Property<T>(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            var actualType = typeof(T).GetActualType();

            switch (actualType)
            {
                case Type type when type == typeof(string):
                    return Property(name).AsString.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(Guid):
                    return Property(name).AsGuid.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(TimeSpan):
                    return Property(name).AsTimespan.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(short):
                    return Property(name).AsShort.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(int):
                    return Property(name).AsInt.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(long):
                    return Property(name).AsLong.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(byte):
                    return Property(name).AsByte.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(bool):
                    return Property(name).AsBool.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(decimal):
                    return Property(name).AsDecimal.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(float):
                    return Property(name).AsFloat.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(double):
                    return Property(name).AsDouble.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                case Type type when type == typeof(DateTime):
                    return Property(name).AsDate.CastTo<IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder>>();
                default:
                    throw new NotSupportedException($"Type {actualType} is not a queryable type. If an enum was used use the EnumProperty<T>(string name)");
            }
        }

        /// <summary>
        /// Adds a condition on the property of a background job where the expected type of the property is enum <typeparamref name="T"/>.
        /// </summary>
        /// <param name="name">The name of the background job property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder> EnumProperty<T>(string name) where T : struct, Enum => Property(name).AsEnum<T>();
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
    }

    /// <summary>
    /// Builder for defining conditions on a (job or state) property.
    /// </summary>
    public interface IQueryJobPropertyConditionBuilder
    {
        /// <summary>
        /// Property should exist.
        /// </summary>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> Exists { get; }
        /// <summary>
        /// Property should not exist.
        /// </summary>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> NotExists { get; }
        /// <summary>
        /// Queries a property of type <see cref="string"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> AsString { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<Guid, IQueryJobConditionBuilder> AsGuid { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<TimeSpan, IQueryJobConditionBuilder> AsTimespan { get; }
        /// <summary>
        /// Queries a property of type <see cref="Enum"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder> AsEnum<T>() where T : struct, Enum;
        /// <summary>
        /// Queries a property of type <see cref="short"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<short, IQueryJobConditionBuilder> AsShort { get; }
        /// <summary>
        /// Queries a property of type <see cref="int"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<int, IQueryJobConditionBuilder> AsInt { get; }
        /// <summary>
        /// Queries a property of type <see cref="long"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<long, IQueryJobConditionBuilder> AsLong { get; }
        /// <summary>
        /// Queries a property of type <see cref="byte"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<byte, IQueryJobConditionBuilder> AsByte { get; }
        /// <summary>
        /// Queries a property of type <see cref="bool"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<bool, IQueryJobConditionBuilder> AsBool { get; }
        /// <summary>
        /// Queries a property of type <see cref="decimal"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<decimal, IQueryJobConditionBuilder> AsDecimal { get; }
        /// <summary>
        /// Queries a property of type <see cref="float"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<float, IQueryJobConditionBuilder> AsFloat { get; }
        /// <summary>
        /// Queries a property of type <see cref="double"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<double, IQueryJobConditionBuilder> AsDouble { get; }
        /// <summary>
        /// Queries a property of type <see cref="DateTime"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> AsDate { get; }
    }
}
