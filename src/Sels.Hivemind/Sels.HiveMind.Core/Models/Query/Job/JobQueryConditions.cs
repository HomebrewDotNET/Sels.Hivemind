﻿using Sels.Core.Extensions;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Sels.HiveMind.Query.Job
{
    /// <summary>
    /// Contains the parameters for a query launched on jobs.
    /// </summary>
    public class JobQueryConditions : JobConditionGroup
    {
        /// <inheritdoc cref="JobQueryConditions"/>
        /// <param name="builder">Delegate for configuring the current instance</param>
        public JobQueryConditions(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> builder) : base(builder)
        {
            WrapGroup = false;
        }

        /// <inheritdoc cref="JobQueryConditions"/>
        public JobQueryConditions() : base()
        {
            WrapGroup = false;
        }
    }

    /// <summary>
    /// Expression that contains either a <see cref="JobConditionGroup"/> or <see cref="JobCondition"/>.
    /// </summary>
    public class JobConditionExpression
    {
        /// <summary>
        /// True if <see cref="Group"/> is set, otherwise false if <see cref="Condition"/> is set.
        /// </summary>
        public bool IsGroup => Group != null;
        /// <summary>
        /// The condition group for this expression if <see cref="IsGroup"/> is set to true.
        /// </summary>
        public JobConditionGroup Group { get; set; }
        /// <summary>
        /// The condition for this expression if <see cref="IsGroup"/> is set to false.
        /// </summary>
        public JobCondition Condition { get; set; }

        /// <summary>
        /// Adds text representation of the current condition group to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            if(IsGroup && Group != null)
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
    /// Allows expression to be compared to each other in a list.
    /// </summary>
    public class JobConditionGroupExpression
    {
        /// <summary>
        /// Expression that contains the condition or another group.
        /// </summary>
        public JobConditionExpression Expression { get; set; }
        /// <summary>
        /// How to compare <see cref="Expression"/> and any next defined condition.
        /// </summary>
        public QueryLogicalOperator? Operator { get; set; }

        /// <inheritdoc cref="JobConditionGroupExpression"/>
        /// <param name="expression"><inheritdoc cref="Expression"/></param>
        /// <param name="logicalOperator"><inheritdoc cref="Operator"/></param>
        public JobConditionGroupExpression(JobConditionExpression expression, QueryLogicalOperator? logicalOperator = null)
        {
            Expression = expression.ValidateArgument(nameof(expression));
            Operator = logicalOperator;
        }

        /// <inheritdoc cref="JobConditionGroupExpression"/>
        public JobConditionGroupExpression()
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

    /// <summary>
    /// Contains grouped together condition on a background job.
    /// </summary>
    public class JobConditionGroup : IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>
    {
        // Properties
        /// <summary>
        /// Contains the conditions for this group. Last operator will always be null.
        /// </summary>
        public List<JobConditionGroupExpression> Conditions { get; } = new List<JobConditionGroupExpression>();
        /// <inheritdoc/>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> IQueryJobConditionBuilder.Queue
        {
            get
            {
                var queryComparison = new QueryComparison<string, IQueryJobConditionBuilder>(this);
                var expression = new JobConditionExpression()
                {
                    Condition = new JobCondition()
                    {
                        Target = QueryJobConditionTarget.Queue,
                        QueueComparison = queryComparison
                    }
                };
                Conditions.Add(new JobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> IQueryJobConditionBuilder.CreatedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryJobConditionBuilder>(this);
                var expression = new JobConditionExpression()
                {
                    Condition = new JobCondition()
                    {
                        Target = QueryJobConditionTarget.CreatedAt,
                        CreatedAtComparison = queryComparison
                    }
                };
                Conditions.Add(new JobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> IQueryJobConditionBuilder.ModifiedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryJobConditionBuilder>(this);
                var expression = new JobConditionExpression()
                {
                    Condition = new JobCondition()
                    {
                        Target = QueryJobConditionTarget.ModifiedAt,
                        ModifiedAtComparison = queryComparison
                    }
                };
                Conditions.Add(new JobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryJobStateConditionBuilder IQueryJobConditionBuilder.CurrentState
        {
            get
            {
                var stateCondition = new JobStateCondition(this);
                var expression = new JobConditionExpression()
                {
                    Condition = new JobCondition()
                    {
                        Target = QueryJobConditionTarget.CurrentState,
                        CurrentStateComparison = stateCondition
                    }
                };
                Conditions.Add(new JobConditionGroupExpression(expression));
                return stateCondition;
            }
        }
        /// <inheritdoc/>
        IQueryJobStateConditionBuilder IQueryJobConditionBuilder.PastState
        {
            get
            {
                var stateCondition = new JobStateCondition(this);
                var expression = new JobConditionExpression()
                {
                    Condition = new JobCondition()
                    {
                        Target = QueryJobConditionTarget.PastState,
                        PastStateComparison = stateCondition
                    }
                };
                Conditions.Add(new JobConditionGroupExpression(expression));
                return stateCondition;
            }
        }
        /// <inheritdoc/>
        IQueryJobConditionBuilder IChainedQueryConditionBuilder<IQueryJobConditionBuilder>.And
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
        IQueryJobConditionBuilder IChainedQueryConditionBuilder<IQueryJobConditionBuilder>.Or
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
        /// If the current group should be wrapped in () when convrting to a string.
        /// </summary>
        protected bool WrapGroup { get; set; } = true;

        /// <inheritdoc cref="JobConditionGroup"/>
        /// <param name="builder">Delegate for configuring the current instance</param>
        public JobConditionGroup(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> builder)
        {
            builder.ValidateArgument(nameof(builder));
            builder(this);
        }
        /// <inheritdoc cref="JobConditionGroup"/>
        protected JobConditionGroup()
        {

        }

        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> IQueryJobConditionBuilder.Group(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> builder)
        {
            builder.ValidateArgument(nameof(builder));

            var group = new JobConditionGroup(builder);
            if (group.Conditions.HasValue()) Conditions.Add(new JobConditionGroupExpression(new JobConditionExpression() { Group = group }));

            return this;
        }
        /// <inheritdoc/>
        IQueryJobPropertyConditionBuilder IQueryJobConditionBuilder.Property(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var propertyBuilder = new JobPropertyCondition(this)
            {
                Name = name
            };

            var expression = new JobConditionExpression()
            {
                Condition = new JobCondition()
                {
                    Target = QueryJobConditionTarget.Property,
                    PropertyComparison = propertyBuilder
                }
            };
            Conditions.Add(new JobConditionGroupExpression(expression));
            return propertyBuilder;
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
    public class JobCondition
    {
        /// <summary>
        /// Defines what the condition is placed on.
        /// </summary>
        public QueryJobConditionTarget Target { get; set; }
        /// <summary>
        /// How to compare the queue on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.Queue"/>.
        /// </summary>
        public QueryComparison QueueComparison { get; set; }
        /// <summary>
        /// How to compare the holder of a lock on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.LockedBy"/>.
        /// </summary>
        public QueryComparison LockedByComparison { get; set; }
        /// <summary>
        /// How to compare the priority on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.Priority"/>.
        /// </summary>
        public QueryComparison PriorityComparison { get; set; }
        /// <summary>
        /// How to compare the current state on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.CurrentState"/>.
        /// </summary>
        public JobStateCondition CurrentStateComparison { get; set; }
        /// <summary>
        /// How to compare a past state on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.PastState"/>.
        /// </summary>
        public JobStateCondition PastStateComparison { get; set; }
        /// <summary>
        /// How to compare a past or current state on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.AnyState"/>.
        /// </summary>
        public JobStateCondition AnyStateComparison { get; set; }
        /// <summary>
        /// How to compare the creation date on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.CreatedAt"/>.
        /// </summary>
        public QueryComparison CreatedAtComparison { get; set; }
        /// <summary>
        /// How to compare the last modification date on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.ModifiedAt"/>.
        /// </summary>
        public QueryComparison ModifiedAtComparison { get; set; }
        /// <summary>
        /// How to compare the value of property on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobConditionTarget.Property"/>.
        /// </summary>
        public JobPropertyCondition PropertyComparison { get; set; }

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
                case QueryJobConditionTarget.Queue:
                    stringBuilder.Append(QueryJobConditionTarget.Queue).AppendSpace();
                    if (QueueComparison != null) QueueComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.LockedBy:
                    stringBuilder.Append(QueryJobConditionTarget.LockedBy).AppendSpace();
                    if (LockedByComparison != null) LockedByComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.Priority:
                    stringBuilder.Append(QueryJobConditionTarget.Priority).AppendSpace();
                    if (PriorityComparison != null) PriorityComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.CurrentState:
                    stringBuilder.Append(QueryJobConditionTarget.CurrentState).Append('.');
                    if (CurrentStateComparison != null) CurrentStateComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.PastState:
                    stringBuilder.Append(QueryJobConditionTarget.PastState).Append('.');
                    if (PastStateComparison != null) PastStateComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.CreatedAt:
                    stringBuilder.Append(QueryJobConditionTarget.CreatedAt).AppendSpace();
                    if (CreatedAtComparison != null) CreatedAtComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.ModifiedAt:
                    stringBuilder.Append(QueryJobConditionTarget.ModifiedAt).AppendSpace();
                    if (ModifiedAtComparison != null) ModifiedAtComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobConditionTarget.Property:
                    stringBuilder.Append("Job").Append('.');
                    if (PropertyComparison != null) PropertyComparison.ToString(stringBuilder, ref index);
                    break;
            }
        }
    }

    /// <summary>
    /// Contains the condition on something of a background job state.
    /// </summary>
    public class JobStateCondition : IQueryJobStateConditionBuilder
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<IQueryJobConditionBuilder> _parent;

        /// <summary>
        /// Defines what the condition is placed on.
        /// </summary>
        public QueryJobStateConditionTarget Target { get; set; }
        /// <summary>
        /// How to compare the name on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobStateConditionTarget.Name"/>.
        /// </summary>
        public QueryComparison NameComparison { get; set; }
        /// <summary>
        /// How to compare the reason on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobStateConditionTarget.Reason"/>.
        /// </summary>
        public QueryComparison ReasonComparison { get; set; }
        /// <summary>
        /// How to compare the elected date on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobStateConditionTarget.Reason"/>.
        /// </summary>
        public QueryComparison ElectedDateComparison { get; set; }
        /// <summary>
        /// How to compare the value of property on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryJobStateConditionTarget.Property"/>.
        /// </summary>
        public JobPropertyCondition PropertyComparison { get; set; }

        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> IQueryJobStateConditionBuilder.Name
        {
            get
            {
                var queryComparison = new QueryComparison<string, IQueryJobConditionBuilder>(_parent);
                Target = QueryJobStateConditionTarget.Name;
                NameComparison = queryComparison;
                return queryComparison;
            }
        }

        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> IQueryJobStateConditionBuilder.ElectedDate
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryJobConditionBuilder>(_parent);
                Target = QueryJobStateConditionTarget.ElectedDate;
                ElectedDateComparison = queryComparison;
                return queryComparison;
            }
        }

        /// <inheritdoc cref="JobStateCondition"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public JobStateCondition(IChainedQueryConditionBuilder<IQueryJobConditionBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }

        /// <inheritdoc cref="JobStateCondition"/>
        public JobStateCondition()
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
                case QueryJobStateConditionTarget.Name:
                    stringBuilder.Append(QueryJobStateConditionTarget.Name).AppendSpace();
                    if (NameComparison != null) NameComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobStateConditionTarget.Reason:
                    stringBuilder.Append(QueryJobStateConditionTarget.Reason).AppendSpace();
                    if (ReasonComparison != null) ReasonComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobStateConditionTarget.ElectedDate:
                    stringBuilder.Append(QueryJobStateConditionTarget.ElectedDate).AppendSpace();
                    if (ElectedDateComparison != null) ElectedDateComparison.ToString(stringBuilder, ref index);
                    break;
                case QueryJobStateConditionTarget.Property:
                    if (PropertyComparison != null) PropertyComparison.ToString(stringBuilder, ref index);
                    break;
            }
        }
    }

    /// <summary>
    /// Contains the condition on a background job or state property.
    /// </summary>
    public class JobPropertyCondition : IQueryJobPropertyConditionBuilder
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<IQueryJobConditionBuilder> _parent;

        // Properties
        /// <summary>
        /// The name of the property the condition is placed on.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The storage type of the property to query.
        /// </summary>
        public StorageType Type { get; set; }
        /// <summary>
        /// How the current property should be queried.
        /// </summary>
        public JobPropertyConditionQueryType QueryType { get; set; }
        /// <summary>
        /// How to compare the property value to form a condition.
        /// </summary>
        public QueryComparison Comparison { get; set; }

        /// <inheritdoc/>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsString
        {
            get
            {
                var queryComparison = new QueryComparison<string, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(string));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionTextComparisonBuilder<Guid, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsGuid
        {
            get
            {
                var queryComparison = new QueryComparison<Guid, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(Guid?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<short, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsShort
        {
            get
            {
                var queryComparison = new QueryComparison<short, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(short?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<int, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsInt
        {
            get
            {
                var queryComparison = new QueryComparison<int, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(int?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<long, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsLong
        {
            get
            {
                var queryComparison = new QueryComparison<long, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(long?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<byte, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsByte
        {
            get
            {
                var queryComparison = new QueryComparison<byte, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(byte?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<bool, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsBool
        {
            get
            {
                var queryComparison = new QueryComparison<bool, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(bool?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<decimal, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsDecimal
        {
            get
            {
                var queryComparison = new QueryComparison<decimal, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(decimal?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<float, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsFloat
        {
            get
            {
                var queryComparison = new QueryComparison<float, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(float?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<double, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsDouble
        {
            get
            {
                var queryComparison = new QueryComparison<double, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(double?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsDate
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(DateTime?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<TimeSpan, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsTimespan
        {
            get
            {
                var queryComparison = new QueryComparison<TimeSpan, IQueryJobConditionBuilder>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(TimeSpan?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        public IChainedQueryConditionBuilder<IQueryJobConditionBuilder> Exists
        {
            get
            {
                QueryType = JobPropertyConditionQueryType.Exists;
                return _parent;
            }
        }
        /// <inheritdoc/>
        public IChainedQueryConditionBuilder<IQueryJobConditionBuilder> NotExists
        {
            get
            {
                QueryType = JobPropertyConditionQueryType.NotExists;
                return _parent;
            }
        }

        /// <inheritdoc cref="JobPropertyCondition"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public JobPropertyCondition(IChainedQueryConditionBuilder<IQueryJobConditionBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }
        /// <inheritdoc cref="JobPropertyCondition"/>
        public JobPropertyCondition()
        {

        }

        IQueryConditionTextComparisonBuilder<T, IQueryJobConditionBuilder> IQueryJobPropertyConditionBuilder.AsEnum<T>()
        {
            var queryComparison = new QueryComparison<T, IQueryJobConditionBuilder>(_parent);
            Type = HiveMindHelper.Storage.GetStorageType(typeof(T?));
            Comparison = queryComparison;
            return queryComparison;
        }

        /// <summary>
        /// Adds text representation of the current condition to <paramref name="index"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        public void ToString(StringBuilder stringBuilder, ref int index)
        {
            stringBuilder.ValidateArgument(nameof(stringBuilder));

            stringBuilder.Append(Name).Append('(').Append(Type).Append(')').AppendSpace();

            switch (QueryType)
            {
                case JobPropertyConditionQueryType.Exists:
                    stringBuilder.Append(JobPropertyConditionQueryType.Exists);
                    break;
                case JobPropertyConditionQueryType.NotExists:
                    stringBuilder.Append(JobPropertyConditionQueryType.NotExists);
                    break;
                case JobPropertyConditionQueryType.Value:
                    if (Comparison != null) Comparison.ToString(stringBuilder, ref index);
                    else stringBuilder.Append("NULL");
                    break;
            }
        }
    }
    /// <summary>
    /// Determines how a job property should be queried.
    /// </summary>
    public enum JobPropertyConditionQueryType
    {
        /// <summary>
        /// Value of the property should be compared.
        /// </summary>
        Value = 0,
        /// <summary>
        /// Condition should check if property exists.
        /// </summary>
        Exists = 1,
        /// <summary>
        /// Condition should check if property is missing.
        /// </summary>
        NotExists = 2
    }
}