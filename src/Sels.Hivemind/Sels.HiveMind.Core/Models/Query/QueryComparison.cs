using Newtonsoft.Json.Linq;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Contains how a value should be compred in a query condition.
    /// </summary>
    public class QueryComparison
    {
        /// <summary>
        /// True if the comparison result should be inverted. So true becomes false and false becomes true.
        /// </summary>
        public bool IsInverted { get; set; }
        /// <summary>
        /// How the value is compared.
        /// </summary>
        public QueryComparator Comparator { get; set; }
        /// <summary>
        /// The value to compare to. 
        /// Will be set when <see cref="Comparator"/> is not set to <see cref="QueryComparator.In"/> or <see cref="QueryComparator.Like"/>.
        /// </summary>
        public object Value { get; set; }
        /// <summary>
        /// The values to compare to. 
        /// Will be set when <see cref="Comparator"/> is set to <see cref="QueryComparator.In"/>.
        /// </summary>
        public object[] Values { get; set; }
        /// <summary>
        /// The pattern to compare to. Original pattern is split up into string elements and elements that equal <see cref="HiveMindConstants.Query.Wildcard"/>.
        /// Will be set when <see cref="Comparator"/> is set to <see cref="QueryComparator.Like"/>.
        /// </summary>
        public string[] Pattern { get; set; }
    }

    /// <summary>
    /// Contains how a value should be compred in a query condition.
    /// Helper class with builder support.
    /// </summary>
    public class QueryComparison<T> : QueryComparison, IQueryConditionTextComparisonBuilder<T>
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> _parent;

        // Properties
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<T> IQueryConditionComparisonBuilder<T>.Not { 
            get
            {
                IsInverted = true; 
                return this;
            }
        }

        /// <inheritdoc cref="QueryComparison{T}"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public QueryComparison(IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }

        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionComparisonBuilder<T>.EqualTo(T value)
        {
            Value = value;
            Comparator = QueryComparator.Equals;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionComparisonBuilder<T>.GreaterOrEqualTo(T value)
        {
            Value = value.ValidateArgument(nameof(value));
            Comparator = QueryComparator.GreaterOrEqualTo;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionComparisonBuilder<T>.GreaterThan(T value)
        {
            Value = value.ValidateArgument(nameof(value));
            Comparator = QueryComparator.GreaterThan;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionComparisonBuilder<T>.In(IEnumerable<T> values)
        {
            Values = values.ValidateArgumentNotNullOrEmpty(nameof(values)).OfType<object>().ToArray();
            Comparator = QueryComparator.In;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionComparisonBuilder<T>.LesserOrEqualTo(T value)
        {
            Value = value.ValidateArgument(nameof(value));
            Comparator = QueryComparator.LesserOrEqualTo;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionComparisonBuilder<T>.LesserThan(T value)
        {
            Value = value.ValidateArgument(nameof(value));
            Comparator = QueryComparator.LesserThan;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryConditionTextComparisonBuilder<T>.Like(string pattern)
        {
            pattern.ValidateArgumentNotNullOrWhitespace(nameof(pattern));
            if (!pattern.Contains(HiveMindConstants.Query.Wildcard)) throw new InvalidOperationException($"Pattern <{pattern}> did not contain any wildcard character (e.g. {HiveMindConstants.Query.Wildcard})");

            var escapedWildcard = HiveMindConstants.Query.WildcardEscape.ToString() + HiveMindConstants.Query.Wildcard.ToString();
            var wildcard = HiveMindConstants.Query.Wildcard.ToString();

            List<string> patternElements = new List<string>();
            int lastPosition = 0;
            foreach(Match match in Regex.Matches(pattern, HiveMindConstants.Query.WildcardRegex))
            {
                var patternElement = pattern.Substring(lastPosition, match.Index-lastPosition);
                if(!patternElement.IsNullOrEmpty()) patternElements.Add(patternElement.Replace(escapedWildcard, wildcard));
                patternElements.Add(HiveMindConstants.Query.Wildcard.ToString());
                lastPosition = match.Index + match.Length;
            }
            var lastPatternElement = pattern.Substring(lastPosition);
            if (!lastPatternElement.IsNullOrEmpty()) patternElements.Add(lastPatternElement.Replace(escapedWildcard, wildcard));

            Pattern = patternElements.ToArray();
            Comparator = QueryComparator.Like;
            return _parent;
        }
    }
}
