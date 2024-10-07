using Sels.HiveMind.Client;

namespace Sels.HiveMind.Client.Query
{
    /// <summary>
    /// Builder for defining conditions on a property attached to an entity (job, state, colony, daemon, ...)
    /// </summary>
    /// <typeparam name="TReturn">The type of builder to return for the fluent syntax</typeparam>
    public interface IQueryPropertyConditionBuilder<TReturn>
    {
        /// <summary>
        /// Property should exist.
        /// </summary>
        IChainedQueryConditionBuilder<TReturn> Exists { get; }
        /// <summary>
        /// Property should not exist.
        /// </summary>
        IChainedQueryConditionBuilder<TReturn> NotExists { get; }
        /// <summary>
        /// Queries a property of type <see cref="string"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, TReturn> AsString { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<Guid, TReturn> AsGuid { get; }
        /// <summary>
        /// Queries a property of type <see cref="Guid"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<TimeSpan, TReturn> AsTimespan { get; }
        /// <summary>
        /// Queries a property of type <see cref="Enum"/>.
        /// </summary>
        IQueryConditionTextComparisonBuilder<T, TReturn> AsEnum<T>() where T : struct, Enum;
        /// <summary>
        /// Queries a property of type <see cref="short"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<short, TReturn> AsShort { get; }
        /// <summary>
        /// Queries a property of type <see cref="int"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<int, TReturn> AsInt { get; }
        /// <summary>
        /// Queries a property of type <see cref="long"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<long, TReturn> AsLong { get; }
        /// <summary>
        /// Queries a property of type <see cref="byte"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<byte, TReturn> AsByte { get; }
        /// <summary>
        /// Queries a property of type <see cref="bool"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<bool, TReturn> AsBool { get; }
        /// <summary>
        /// Queries a property of type <see cref="decimal"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<decimal, TReturn> AsDecimal { get; }
        /// <summary>
        /// Queries a property of type <see cref="float"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<float, TReturn> AsFloat { get; }
        /// <summary>
        /// Queries a property of type <see cref="double"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<double, TReturn> AsDouble { get; }
        /// <summary>
        /// Queries a property of type <see cref="DateTime"/>.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, TReturn> AsDate { get; }
    }
}
