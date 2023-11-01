using Sels.Core;
using Sels.Core.Extensions.Fluent;
using Sels.HiveMind.Client;
using System.Collections.Generic;
using System.Linq;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for defining how to compare a value.
    /// </summary>
    /// <typeparam name="T">The type of value that can be compared to</typeparam>
    public interface IQueryConditionComparisonBuilder<T>
    {
        /// <summary>
        /// Invert the next created condition. So true becomes false and false becomes true.
        /// </summary>
        IQueryConditionComparisonBuilder<T> Not { get; }

        /// <summary>
        /// Value should be equal to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> EqualTo(T value);
        /// <summary>
        /// Value should be greater than <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> GreaterThan(T value);
        /// <summary>
        /// Value should be lesser than <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> LesserThan(T value);
        /// <summary>
        /// Value should be greater or equal to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> GreaterOrEqualTo(T value);
        /// <summary>
        /// Value should be lesser or equal to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> LesserOrEqualTo(T value);
        /// <summary>
        /// Value should be equal to any value in <paramref name="values"/>.
        /// </summary>
        /// <param name="values">The values to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> In(IEnumerable<T> values);
        /// <summary>
        /// Value should be equal to <paramref name="value"/> or any value in <paramref name="values"/>.
        /// </summary>
        /// <param name="value">The value to compare to</param>
        /// <param name="values">The values to compare to</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> In(T value, params T[] values) => In(Helper.Collection.Enumerate(value, values).Where(x => x != null));
    }

    /// <summary>
    /// Builder for defining how to compare a text value.
    /// </summary>
    /// <typeparam name="T">The type of value that can be compared to</typeparam>
    public interface IQueryConditionTextComparisonBuilder<T> : IQueryConditionComparisonBuilder<T>
    {
        /// <summary>
        /// Value should be like pattern <paramref name="pattern"/>. Wildcards can be defined using <see cref="HiveMindConstants.Query.Wildcard"/>.
        /// </summary>
        /// <param name="pattern">The pattern the value must match</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> Like(string pattern);
    }
}
