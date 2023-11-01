using Sels.Core.Extensions;
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
    /// Contains the parameters for a query launched on background jobs.
    /// </summary>
    public class BackgroundJobQueryConditions : BackgroundJobConditionGroup
    {
        /// <inheritdoc cref="BackgroundJobQueryConditions"/>
        /// <param name="builder">Delegate for configuring the current instance</param>
        public BackgroundJobQueryConditions(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> builder) : base(builder)
        {
        }

        /// <inheritdoc cref="BackgroundJobQueryConditions"/>
        public BackgroundJobQueryConditions() : base()
        {
        }
    }

    /// <summary>
    /// Expression that contains either a <see cref="BackgroundJobConditionGroup"/> or <see cref="BackgroundJobCondition"/>.
    /// </summary>
    public class BackgroundJobConditionExpression
    {
        /// <summary>
        /// True if <see cref="Group"/> is set, otherwise false if <see cref="Condition"/> is set.
        /// </summary>
        public bool IsGroup => Group != null;
        /// <summary>
        /// The condition group for this expression if <see cref="IsGroup"/> is set to true.
        /// </summary>
        public BackgroundJobConditionGroup Group { get; set; }
        /// <summary>
        /// The condition for this expression if <see cref="IsGroup"/> is set to false.
        /// </summary>
        public BackgroundJobCondition Condition { get; set; }
    }

    /// <summary>
    /// Allows expression to be compared to each other in a list.
    /// </summary>
    public class BackgroundJobConditionGroupExpression
    {
        /// <summary>
        /// Expression that contains the condition or another group.
        /// </summary>
        public BackgroundJobConditionExpression Expression { get; set; }
        /// <summary>
        /// How to compare <see cref="Expression"/> and any next defined condition.
        /// </summary>
        public QueryLogicalOperator? Operator { get; set; }

        /// <inheritdoc cref="BackgroundJobConditionGroupExpression"/>
        /// <param name="expression"><inheritdoc cref="Expression"/></param>
        /// <param name="logicalOperator"><inheritdoc cref="Operator"/></param>
        public BackgroundJobConditionGroupExpression(BackgroundJobConditionExpression expression, QueryLogicalOperator? logicalOperator = null)
        {
            Expression = expression.ValidateArgument(nameof(expression));
            Operator = logicalOperator;
        }

        /// <inheritdoc cref="BackgroundJobConditionGroupExpression"/>
        public BackgroundJobConditionGroupExpression()
        {
            
        }
    }

    /// <summary>
    /// Contains grouped together condition on a background job.
    /// </summary>
    public class BackgroundJobConditionGroup : IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>
    {
        // Properties
        /// <summary>
        /// Contains the conditions for this group. Last operator will always be null.
        /// </summary>
        public List<BackgroundJobConditionGroupExpression> Conditions { get; } = new List<BackgroundJobConditionGroupExpression>();
        /// <inheritdoc/>
        IQueryConditionTextComparisonBuilder<string> IQueryBackgroundJobConditionBuilder.Queue
        {
            get
            {
                var queryComparison = new QueryComparison<string>(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.Queue,
                        QueueComparison = queryComparison
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<QueuePriority> IQueryBackgroundJobConditionBuilder.Priority
        {
            get
            {
                var queryComparison = new QueryComparison<QueuePriority>(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.Priority,
                        PriorityComparison = queryComparison
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<DateTime> IQueryBackgroundJobConditionBuilder.CreatedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime>(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.CreatedAt,
                        CreatedAtComparison = queryComparison
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<DateTime> IQueryBackgroundJobConditionBuilder.ModifiedAt
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime>(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.ModifiedAt,
                        ModifiedAtComparison = queryComparison
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        IQueryConditionTextComparisonBuilder<string> IQueryBackgroundJobConditionBuilder.LockedBy
        {
            get
            {
                var queryComparison = new QueryComparison<string>(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.LockedBy,
                        LockedByComparison = queryComparison
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryBackgroundJobStateConditionBuilder IQueryBackgroundJobConditionBuilder.CurrentState
        {
            get
            {
                var stateCondition = new BackgroundJobStateCondition(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.CurrentState,
                        CurrentStateComparison = stateCondition
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return stateCondition;
            }
        }
        /// <inheritdoc/>
        IQueryBackgroundJobStateConditionBuilder IQueryBackgroundJobConditionBuilder.PastState
        {
            get
            {
                var stateCondition = new BackgroundJobStateCondition(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.PastState,
                        PastStateComparison = stateCondition
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return stateCondition;
            }
        }
        /// <inheritdoc/>
        IQueryBackgroundJobStateConditionBuilder IQueryBackgroundJobConditionBuilder.AnyState
        {
            get
            {
                var stateCondition = new BackgroundJobStateCondition(this);
                var expression = new BackgroundJobConditionExpression()
                {
                    Condition = new BackgroundJobCondition()
                    {
                        Target = QueryBackgroundJobConditionTarget.AnyState,
                        AnyStateComparison = stateCondition
                    }
                };
                Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
                return stateCondition;
            }
        }
        /// <inheritdoc/>
        IQueryBackgroundJobConditionBuilder IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>.And
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
        IQueryBackgroundJobConditionBuilder IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>.Or
        {
            get
            {
                if (!Conditions.HasValue()) throw new InvalidOperationException($"Expected conditions to be set but list was empty.");

                var lastCondition = Conditions.Last();
                lastCondition.Operator = QueryLogicalOperator.Or;
                return this;
            }
        }

        

        /// <inheritdoc cref="BackgroundJobConditionGroup"/>
        /// <param name="builder">Delegate for configuring the current instance</param>
        public BackgroundJobConditionGroup(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> builder)
        {
            builder.ValidateArgument(nameof(builder));
            builder(this);
        }
        /// <inheritdoc cref="BackgroundJobConditionGroup"/>
        protected BackgroundJobConditionGroup()
        {

        }

        /// <inheritdoc/>
        IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> IQueryBackgroundJobConditionBuilder.Group(Func<IQueryBackgroundJobConditionBuilder, IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder>> builder)
        {
            builder.ValidateArgument(nameof(builder));

            var group = new BackgroundJobConditionGroup(builder);
            if (group.Conditions.HasValue()) Conditions.Add(new BackgroundJobConditionGroupExpression(new BackgroundJobConditionExpression() { Group = group }));

            return this;
        }
        /// <inheritdoc/>
        IQueryBackgroundJobPropertyConditionBuilder IQueryBackgroundJobConditionBuilder.Property(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var propertyBuilder = new BackgroundJobPropertyCondition(this)
            {
                Name = name
            };

            var expression = new BackgroundJobConditionExpression()
            {
                Condition = new BackgroundJobCondition()
                {
                    Target = QueryBackgroundJobConditionTarget.Property,
                    PropertyComparison = propertyBuilder
                }
            };
            Conditions.Add(new BackgroundJobConditionGroupExpression(expression));
            return propertyBuilder;
        }
    }

    /// <summary>
    /// Contains the condition on something of a background job.
    /// </summary>
    public class BackgroundJobCondition
    {
        /// <summary>
        /// Defines what the condition is placed on.
        /// </summary>
        public QueryBackgroundJobConditionTarget Target { get; set; }
        /// <summary>
        /// How to compare the queue on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.Queue"/>.
        /// </summary>
        public QueryComparison QueueComparison { get; set; }
        /// <summary>
        /// How to compare the holder of a lock on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.LockedBy"/>.
        /// </summary>
        public QueryComparison LockedByComparison { get; set; }
        /// <summary>
        /// How to compare the priority on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.Priority"/>.
        /// </summary>
        public QueryComparison PriorityComparison { get; set; }
        /// <summary>
        /// How to compare the current state on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.CurrentState"/>.
        /// </summary>
        public BackgroundJobStateCondition CurrentStateComparison { get; set; }
        /// <summary>
        /// How to compare a past state on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.PastState"/>.
        /// </summary>
        public BackgroundJobStateCondition PastStateComparison { get; set; }
        /// <summary>
        /// How to compare a past or current state on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.AnyState"/>.
        /// </summary>
        public BackgroundJobStateCondition AnyStateComparison { get; set; }
        /// <summary>
        /// How to compare the creation date on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.CreatedAt"/>.
        /// </summary>
        public QueryComparison CreatedAtComparison { get; set; }
        /// <summary>
        /// How to compare the last modification date on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.ModifiedAt"/>.
        /// </summary>
        public QueryComparison ModifiedAtComparison { get; set; }
        /// <summary>
        /// How to compare the value of property on a background job to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobConditionTarget.Property"/>.
        /// </summary>
        public BackgroundJobPropertyCondition PropertyComparison { get; set; }
    }

    /// <summary>
    /// Contains the condition on something of a background job state.
    /// </summary>
    public class BackgroundJobStateCondition : IQueryBackgroundJobStateConditionBuilder
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> _parent;

        /// <summary>
        /// Defines what the condition is placed on.
        /// </summary>
        public QueryBackgroundJobStateConditionTarget Target { get; set; }
        /// <summary>
        /// How to compare the name on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobStateConditionTarget.Name"/>.
        /// </summary>
        public QueryComparison NameComparison { get; set; }
        /// <summary>
        /// How to compare the reason on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobStateConditionTarget.Reason"/>.
        /// </summary>
        public QueryComparison ReasonComparison { get; set; }
        /// <summary>
        /// How to compare the elected date on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobStateConditionTarget.Reason"/>.
        /// </summary>
        public QueryComparison ElectedDateComparison { get; set; }
        /// <summary>
        /// How to compare the value of property on a background job state to form a condition.
        /// Will be set when <see cref="Target"/> is set to <see cref="QueryBackgroundJobStateConditionTarget.Property"/>.
        /// </summary>
        public BackgroundJobPropertyCondition PropertyComparison { get; set; }

        IQueryConditionTextComparisonBuilder<string> IQueryBackgroundJobStateConditionBuilder.Name
        {
            get
            {
                var queryComparison = new QueryComparison<string>(_parent);
                Target = QueryBackgroundJobStateConditionTarget.Name;
                NameComparison = queryComparison;
                return queryComparison;
            }
        }

        IQueryConditionTextComparisonBuilder<string> IQueryBackgroundJobStateConditionBuilder.Reason
        {
            get
            {
                var queryComparison = new QueryComparison<string>(_parent);
                Target = QueryBackgroundJobStateConditionTarget.Reason;
                ReasonComparison = queryComparison;
                return queryComparison;
            }
        }

        IQueryConditionComparisonBuilder<DateTime> IQueryBackgroundJobStateConditionBuilder.ElectedDate
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime>(_parent);
                Target = QueryBackgroundJobStateConditionTarget.ElectedDate;
                ElectedDateComparison = queryComparison;
                return queryComparison;
            }
        }

        /// <inheritdoc cref="BackgroundJobStateCondition"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public BackgroundJobStateCondition(IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }

        /// <inheritdoc cref="BackgroundJobStateCondition"/>
        public BackgroundJobStateCondition()
        {
        }

        IQueryBackgroundJobPropertyConditionBuilder IQueryBackgroundJobStateConditionBuilder.Property(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            Target = QueryBackgroundJobStateConditionTarget.Property;
            var propertyBuilder = new BackgroundJobPropertyCondition(_parent)
            {
                Name = name
            };
            PropertyComparison = propertyBuilder;
            return propertyBuilder;
        }
    }

    /// <summary>
    /// Contains the condition on a background job or state property.
    /// </summary>
    public class BackgroundJobPropertyCondition : IQueryBackgroundJobPropertyConditionBuilder
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> _parent;

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
        /// How to compare the property value to form a condition.
        /// </summary>
        public QueryComparison Comparison { get; set; }

        /// <inheritdoc/>
        IQueryConditionTextComparisonBuilder<string> IQueryBackgroundJobPropertyConditionBuilder.AsString
        {
            get
            {
                var queryComparison = new QueryComparison<string>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(string));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionTextComparisonBuilder<Guid?> IQueryBackgroundJobPropertyConditionBuilder.AsGuid
        {
            get
            {
                var queryComparison = new QueryComparison<Guid?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(Guid?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<short?> IQueryBackgroundJobPropertyConditionBuilder.AsShort
        {
            get
            {
                var queryComparison = new QueryComparison<short?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(short?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<int?> IQueryBackgroundJobPropertyConditionBuilder.AsInt
        {
            get
            {
                var queryComparison = new QueryComparison<int?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(int?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<long?> IQueryBackgroundJobPropertyConditionBuilder.AsLong
        {
            get
            {
                var queryComparison = new QueryComparison<long?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(long?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<byte?> IQueryBackgroundJobPropertyConditionBuilder.AsByte
        {
            get
            {
                var queryComparison = new QueryComparison<byte?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(byte?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<bool?> IQueryBackgroundJobPropertyConditionBuilder.AsBool
        {
            get
            {
                var queryComparison = new QueryComparison<bool?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(bool?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<decimal?> IQueryBackgroundJobPropertyConditionBuilder.AsDecimal
        {
            get
            {
                var queryComparison = new QueryComparison<decimal?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(decimal?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<float?> IQueryBackgroundJobPropertyConditionBuilder.AsFloat
        {
            get
            {
                var queryComparison = new QueryComparison<float?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(float?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<double?> IQueryBackgroundJobPropertyConditionBuilder.AsDouble
        {
            get
            {
                var queryComparison = new QueryComparison<double?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(double?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<DateTime?> IQueryBackgroundJobPropertyConditionBuilder.AsDate
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(DateTime?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>
        IQueryConditionComparisonBuilder<TimeSpan?> IQueryBackgroundJobPropertyConditionBuilder.AsTimespan
        {
            get
            {
                var queryComparison = new QueryComparison<TimeSpan?>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(TimeSpan?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }

        /// <inheritdoc cref="BackgroundJobPropertyCondition"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public BackgroundJobPropertyCondition(IChainedQueryConditionBuilder<IQueryBackgroundJobConditionBuilder> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }
        /// <inheritdoc cref="BackgroundJobPropertyCondition"/>
        public BackgroundJobPropertyCondition()
        {

        }

        IQueryConditionTextComparisonBuilder<T?> IQueryBackgroundJobPropertyConditionBuilder.AsEnum<T>()
        {
            var queryComparison = new QueryComparison<T?>(_parent);
            Type = HiveMindHelper.Storage.GetStorageType(typeof(T?));
            Comparison = queryComparison;
            return queryComparison;
        }
    }
}
