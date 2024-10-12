using Sels.Core.Extensions.Text;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sels.HiveMind.Client.Query;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Allows expression to be compared to each other in a list.
    /// </summary>
    /// <typeparam name="T">The type of expression used in the group</typeparam>
    public class QueryGroupConditionExpression<T> : IQueryExpression
        where T : IQueryExpression
    {
        /// <summary>
        /// Expression that contains the condition or another group.
        /// </summary>
        public T Expression { get; set; }
        /// <summary>
        /// How to compare <see cref="Expression"/> and any next defined condition.
        /// </summary>
        public QueryLogicalOperator? Operator { get; set; }

        /// <inheritdoc cref="QueryGroupConditionExpression"/>
        /// <param name="expression"><inheritdoc cref="Expression"/></param>
        /// <param name="logicalOperator"><inheritdoc cref="Operator"/></param>
        public QueryGroupConditionExpression(T expression, QueryLogicalOperator? logicalOperator = null)
        {
            Expression = expression.ValidateArgument(nameof(expression));
            Operator = logicalOperator;
        }

        /// <inheritdoc cref="JobConditionGroupExpression"/>
        public QueryGroupConditionExpression()
        {

        }

        /// <summary>
        /// Adds text representation of the current condition group to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            if (Expression != null) Expression.ToString(stringBuilder, ref index);
            if (Operator != null) stringBuilder.AppendSpace().Append(Operator);
        }
    }
}
