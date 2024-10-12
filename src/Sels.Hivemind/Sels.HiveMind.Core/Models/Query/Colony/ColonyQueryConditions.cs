﻿using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client.Query;
using Sels.HiveMind.Client;
using Sels.HiveMind.Query;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Query.Job;
using System.Reflection.Emit;
using Sels.ObjectValidationFramework.Validators;
using System.Diagnostics;

namespace Sels.HiveMind.Query.Colony
{
    /// <summary>
    /// Contains the parameters for a query launched on colonies.
    /// </summary>
    public class ColonyQueryConditions : ColonyConditionGroup
    {
        /// <inheritdoc cref="JobQueryConditions"/>
        /// <param name="builder">Delegate for configuring the current instance</param>
        public ColonyQueryConditions(Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>> builder) : base(builder)
        {
            WrapGroup = false;
        }

        /// <inheritdoc cref="ColonyQueryConditions"/>
        public ColonyQueryConditions() : base()
        {
            WrapGroup = false;
        }
    }

    /// <summary>
    /// Expression that contains either a <see cref="ColonyConditionGroup"/> or <see cref="ColonyCondition"/>.
    /// </summary>
    public class ColonyConditionExpression : IQueryExpression
    {
        /// <summary>
        /// True if <see cref="Group"/> is set, otherwise false if <see cref="Condition"/> is set.
        /// </summary>
        public bool IsGroup => Group != null;
        /// <summary>
        /// The condition group for this expression if <see cref="IsGroup"/> is set to true.
        /// </summary>
        public ColonyConditionGroup Group { get; set; }
        /// <summary>
        /// The condition for this expression if <see cref="IsGroup"/> is set to false.
        /// </summary>
        public ColonyCondition Condition { get; set; }

        /// <summary>
        /// Adds text representation of the current condition group to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            if (IsGroup && Group != null)
            {
                Group.ToString(stringBuilder, ref index);
            }
            else if (Condition != null)
            {
                Condition.ToString(stringBuilder, ref index);
            }
        }
    }

    /// <summary>
    /// Contains grouped together condition on a background job.
    /// </summary>
    public class ColonyConditionGroup : IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>
    {
        // Properties
        /// <summary>
        /// Contains the conditions for this group. Last operator will always be null.
        /// </summary>
        public List<QueryGroupConditionExpression<ColonyConditionExpression>> Conditions { get; } = new List<QueryGroupConditionExpression<ColonyConditionExpression>>();

        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionTextComparisonBuilder<string, IQueryColonyConditionBuilder> IQueryColonyConditionBuilder.Id
        {
            get
            {
                var queryComparison = new QueryComparison<string, IQueryColonyConditionBuilder>(this);
                var expression = new ColonyConditionExpression()
                {
                    Condition = new ColonyCondition()
                    {
                        Target = QueryColonyConditionTarget.Id,
                        IdComparison = queryComparison
                    }
                };
                Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionTextComparisonBuilder<string, IQueryColonyConditionBuilder> IQueryColonyConditionBuilder.Name
        {
            get
            {
                var queryComparison = new QueryComparison<string, IQueryColonyConditionBuilder>(this);
                var expression = new ColonyConditionExpression()
                {
                    Condition = new ColonyCondition()
                    {
                        Target = QueryColonyConditionTarget.Name,
                        NameComparison = queryComparison
                    }
                };
                Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<ColonyStatus, IQueryColonyConditionBuilder> IQueryColonyConditionBuilder.Status
        {
            get
            {
                var queryComparison = new QueryComparison<ColonyStatus, IQueryColonyConditionBuilder>(this);
                var expression = new ColonyConditionExpression()
                {
                    Condition = new ColonyCondition()
                    {
                        Target = QueryColonyConditionTarget.Status,
                        NameComparison = queryComparison
                    }
                };
                Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> IQueryColonyConditionBuilder.CreatedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryColonyConditionBuilder>(this);
                var expression = new ColonyConditionExpression()
                {
                    Condition = new ColonyCondition()
                    {
                        Target = QueryColonyConditionTarget.CreatedAt,
                        NameComparison = queryComparison
                    }
                };
                Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> IQueryColonyConditionBuilder.ModifiedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryColonyConditionBuilder>(this);
                var expression = new ColonyConditionExpression()
                {
                    Condition = new ColonyCondition()
                    {
                        Target = QueryColonyConditionTarget.ModifiedAt,
                        NameComparison = queryComparison
                    }
                };
                Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryPropertyConditionBuilder<IQueryColonyConditionBuilder> IQueryPropertyBuilder<IQueryColonyConditionBuilder>.Property(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var propertyBuilder = new PropertyCondition<IQueryColonyConditionBuilder>(this)
            {
                Name = name
            };

            var expression = new ColonyConditionExpression()
            {
                Condition = new ColonyCondition()
                {
                    Target = QueryColonyConditionTarget.Property,
                    PropertyComparison = propertyBuilder
                }
            };
            Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
            return propertyBuilder;
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IQueryColonyDaemonConditionBuilder Daemon { 
            get
            {
                var daemonCondition = new ColonyDaemonCondition(this);
                var expression = new ColonyConditionExpression()
                {
                    Condition = new ColonyCondition()
                    {
                        Target = QueryColonyConditionTarget.Daemon,
                        DaemonCondition = daemonCondition
                    }
                };
                Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(expression));
                return daemonCondition;
            } 
        }

        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryColonyConditionBuilder IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>.And
        {
            get
            {
                if (!Conditions.HasValue()) throw new InvalidOperationException($"Expected conditions to be set but list was empty.");

                var lastCondition = Conditions.Last();
                lastCondition.Operator = QueryLogicalOperator.And;
                return this;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryColonyConditionBuilder IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>.Or
        {
            get
            {
                if (!Conditions.HasValue()) throw new InvalidOperationException($"Expected conditions to be set but list was empty.");

                var lastCondition = Conditions.Last();
                lastCondition.Operator = QueryLogicalOperator.Or;
                return this;
            }
        }

        /// <summary>
        /// If the current group should be wrapped in () when converting to a string.
        /// </summary>
        protected bool WrapGroup { get; set; } = true;

        /// <inheritdoc cref="ColonyConditionGroup"/>
        /// <param name="builder">Delegate for configuring the current instance</param>
        public ColonyConditionGroup(Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>> builder)
        {
            builder.ValidateArgument(nameof(builder));
            builder(this);
        }
        /// <inheritdoc cref="ColonyConditionGroup"/>
        protected ColonyConditionGroup()
        {

        }

        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryColonyConditionBuilder> IQueryColonyConditionBuilder.Group(Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>> builder)
        {
            builder.ValidateArgument(nameof(builder));

            var group = new ColonyConditionGroup(builder);
            if (group.Conditions.HasValue()) Conditions.Add(new QueryGroupConditionExpression<ColonyConditionExpression>(new ColonyConditionExpression() { Group = group }));

            return this;
        }

        /// <summary>
        /// Returns a text representation of all condition in this group.
        /// </summary>
        /// <returns>A text representation of all condition in this group</returns>
        public override string ToString()
        {
            var index = 0;
            var builder = new StringBuilder();
            ToString(builder, ref index);
            return builder.ToString();
        }

        /// <summary>
        /// Adds text representation of all condition in this group to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            if (Conditions.HasValue())
            {
                if (WrapGroup) stringBuilder.Append('(');
                for (int i = 0; i < Conditions.Count; i++)
                {
                    Conditions[i].ToString(stringBuilder, ref index);

                    if (i != Conditions.Count - 1) stringBuilder.AppendSpace();
                }
                if (WrapGroup) stringBuilder.Append(')');
            }
        }
    }

    /// <summary>
    /// Contains the condition on something of a background job.
    /// </summary>
    public class ColonyCondition
    {
        /// <summary>
        /// Defines what the condition is placed on.
        /// </summary>
        public QueryColonyConditionTarget Target { get; set; }
        /// <summary>
        /// How to compare the id of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Id"/>.
        /// </summary>
        public QueryComparison IdComparison { get; set; }
        /// <summary>
        /// How to compare the name of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Name"/>.
        /// </summary>
        public QueryComparison NameComparison { get; set; }
        /// <summary>
        /// How to compare the status of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Status"/>.
        /// </summary>
        public QueryComparison StatusComparison { get; set; }
        /// <summary>
        /// How to compare the creation date of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.CreatedAt"/>.
        /// </summary>
        public QueryComparison CreatedAtComparison { get; set; }
        /// <summary>
        /// How to compare the last modification date of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.CreatedAt"/>.
        /// </summary>
        public QueryComparison ModifiedAtComparison { get; set; }
        /// <summary>
        /// How to compare something from daemons of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Daemon"/>.
        /// </summary>
        public ColonyDaemonCondition DaemonCondition { get; set; }
        /// <summary>
        /// How to compare a property of a colony to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Property"/>.
        /// </summary>
        public PropertyCondition PropertyComparison { get; set; }

        /// <summary>
        /// Adds text representation of the current condition to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            switch (Target)
            {
                case QueryColonyConditionTarget.Id:
                    stringBuilder.Append(QueryColonyConditionTarget.Id).AppendSpace();
                    if (IdComparison != null) IdComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyConditionTarget.Name:
                    stringBuilder.Append(QueryColonyConditionTarget.Name).AppendSpace();
                    if (NameComparison != null) NameComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyConditionTarget.Status:
                    stringBuilder.Append(QueryColonyConditionTarget.Status).AppendSpace();
                    if (StatusComparison != null) StatusComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyConditionTarget.CreatedAt:
                    stringBuilder.Append(QueryColonyConditionTarget.CreatedAt).AppendSpace();
                    if (CreatedAtComparison != null) CreatedAtComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyConditionTarget.ModifiedAt:
                    stringBuilder.Append(QueryColonyConditionTarget.ModifiedAt).AppendSpace();
                    if (ModifiedAtComparison != null) ModifiedAtComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyConditionTarget.Property:
                    stringBuilder.Append("Colony").Append('.');
                    if (PropertyComparison != null) PropertyComparison.ToString(stringBuilder, ref index);
                    break;
            }
        }
    }

    /// <summary>
    /// Contains the condition on something of a background job state.
    /// </summary>
    public class ColonyDaemonCondition : IQueryColonyDaemonConditionBuilder
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<IQueryColonyConditionBuilder> _parent;

        /// <summary>
        /// Defines what the condition is placed on.
        /// </summary>
        public QueryColonyDaemonConditionTarget Target { get; set; }
        /// <summary>
        /// How to compare the name of a colony daemon to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Name"/>.
        /// </summary>
        public QueryComparison NameComparison { get; set; }
        /// <summary>
        /// How to compare the status of a colony daemon to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Status"/>.
        /// </summary>
        public QueryComparison StatusComparison { get; set; }
        /// <summary>
        /// How to compare the creation date of a colony daemon to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.CreatedAt"/>.
        /// </summary>
        public QueryComparison CreatedAtComparison { get; set; }
        /// <summary>
        /// How to compare the last modification date of a colony daemon to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.CreatedAt"/>.
        /// </summary>
        public QueryComparison ModifiedAtComparison { get; set; }
        /// <summary>
        /// How to compare a property of a colony daemon to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryColonyConditionTarget.Property"/>.
        /// </summary>
        public PropertyCondition PropertyComparison { get; set; }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionTextComparisonBuilder<string, IQueryColonyConditionBuilder> IQueryColonyDaemonConditionBuilder.Name
        {
            get
            {
                var queryComparison = new QueryComparison<string, IQueryColonyConditionBuilder>(_parent);
                Target = QueryColonyDaemonConditionTarget.Name;
                NameComparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<ColonyStatus, IQueryColonyConditionBuilder> IQueryColonyDaemonConditionBuilder.Status
        {
            get
            {
                var queryComparison = new QueryComparison<ColonyStatus, IQueryColonyConditionBuilder>(_parent);
                Target = QueryColonyDaemonConditionTarget.Status;
                StatusComparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> IQueryColonyDaemonConditionBuilder.CreatedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryColonyConditionBuilder>(_parent);
                Target = QueryColonyDaemonConditionTarget.CreatedAt;
                CreatedAtComparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        [IgnoreInValidation(IgnoreType.All)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> IQueryColonyDaemonConditionBuilder.ModifiedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryColonyConditionBuilder>(_parent);
                Target = QueryColonyDaemonConditionTarget.ModifiedAt;
                ModifiedAtComparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryPropertyConditionBuilder<IQueryColonyConditionBuilder> IQueryPropertyBuilder<IQueryColonyConditionBuilder>.Property(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var propertyBuilder = new PropertyCondition<IQueryColonyConditionBuilder>(_parent)
            {
                Name = name
            };
            Target = QueryColonyDaemonConditionTarget.Property;
            PropertyComparison = propertyBuilder;
            return propertyBuilder;
        }

        /// <inheritdoc cref="ColonyDaemonCondition"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public ColonyDaemonCondition(IChainedQueryConditionBuilder<IQueryColonyConditionBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }

        /// <inheritdoc cref="ColonyDaemonCondition"/>
        public ColonyDaemonCondition()
        {
        }

        /// <summary>
        /// Adds text representation of the current condition to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            switch (Target)
            {
                case QueryColonyDaemonConditionTarget.Name:
                    stringBuilder.Append(QueryColonyDaemonConditionTarget.Name).AppendSpace();
                    if (NameComparison != null) NameComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyDaemonConditionTarget.Status:
                    stringBuilder.Append(QueryColonyDaemonConditionTarget.Status).AppendSpace();
                    if (StatusComparison != null) StatusComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyDaemonConditionTarget.CreatedAt:
                    stringBuilder.Append(QueryColonyDaemonConditionTarget.CreatedAt).AppendSpace();
                    if (CreatedAtComparison != null) CreatedAtComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyDaemonConditionTarget.ModifiedAt:
                    stringBuilder.Append(QueryColonyDaemonConditionTarget.ModifiedAt).AppendSpace();
                    if (ModifiedAtComparison != null) ModifiedAtComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryColonyDaemonConditionTarget.Property:
                    stringBuilder.Append("ColonyDaemon").Append('.');
                    if (PropertyComparison != null) PropertyComparison.ToString(stringBuilder, ref index);
                    break;
            }
        }
    }
}