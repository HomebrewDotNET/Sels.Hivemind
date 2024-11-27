using Newtonsoft.Json.Linq;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Text;
using Sels.Core.Models;
using Sels.HiveMind.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Contains how a value should be compared in a query condition.
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

        /// <summary>
        /// Adds text representation of the current comparison to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            if (IsInverted) stringBuilder.Append("NOT").AppendSpace();
            stringBuilder.Append(Comparator).AppendSpace();

            switch(Comparator)
            {
                case QueryComparator.Like:
                    stringBuilder.Append($"@Pattern{index++}");
                    break;
                case QueryComparator.In:
                    stringBuilder.Append('(');
                    if (Values.HasValue())
                    {
                        for(int i = 0; i < Values.Length; i++)
                        {
                            stringBuilder.Append($"Value{index++}");

                            if(i != (Values.Length-1)) stringBuilder.Append(", ");
                        }
                    }
                    stringBuilder.Append(')');
                    break;
                default:
                    if (Value == null) stringBuilder.Append("NULL");
                    else stringBuilder.Append($"@Value{index++}");
                    break;
            }
        }
    }

    /// <summary>
    /// Contains how a value should be compred in a query condition.
    /// Helper class with builder support.
    /// </summary>
    /// <typeparam name="T">The type of value that can be compared to</typeparam>
    /// <typeparam name="TBuilder">The type of builder to return for the fluent syntax</typeparam>
    public class QueryComparison<T, TBuilder> : QueryComparison, IQueryConditionTextComparisonBuilder<T, TBuilder>
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<TBuilder> _parent;

        // Properties
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<T, TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.Not { 
            get
            {
                IsInverted = true; 
                return this;
            }
        }

        /// <inheritdoc cref="QueryComparison{T}"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public QueryComparison(IChainedQueryConditionBuilder<TBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }

        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.EqualTo(T value)
        {
            Value = Guard.IsNotNull(value);
            Comparator = QueryComparator.Equals;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.GreaterOrEqualTo(T value)
        {
            Value = Guard.IsNotNull(value);
            Comparator = QueryComparator.GreaterOrEqualTo;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.GreaterThan(T value)
        {
            Value = Guard.IsNotNull(value);
            Comparator = QueryComparator.GreaterThan;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.In(IEnumerable<T> values)
        {
            Values = values.ValidateArgumentNotNullOrEmpty(nameof(values)).OfType<object>().ToArray();
            Comparator = QueryComparator.In;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.LesserOrEqualTo(T value)
        {
            Value = Guard.IsNotNull(value);
            Comparator = QueryComparator.LesserOrEqualTo;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionComparisonBuilder<T, TBuilder>.LesserThan(T value)
        {
            Value = Guard.IsNotNull(value);
            Comparator = QueryComparator.LesserThan;
            return _parent;
        }
        /// <inheritdoc/>
        IChainedQueryConditionBuilder<TBuilder> IQueryConditionTextComparisonBuilder<T, TBuilder>.Like(string pattern)
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
