using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using Sels.HiveMind.Client.Query;
using Sels.HiveMind.Query;
using Sels.HiveMind.Storage;
using Sels.ObjectValidationFramework.Profile;
using System.Diagnostics;
using System.Text;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Contains the condition on a property from an entity.
    /// </summary>
    [IgnoreInValidation]
    public class PropertyCondition
    {
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
        public PropertyConditionQueryType QueryType { get; set; } = PropertyConditionQueryType.Value;
        /// <summary>
        /// How to compare the property value to form a condition.
        /// </summary>
        [IgnoreInValidation(IgnoreType.None)]
        public QueryComparison Comparison { get; set; }

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
                case PropertyConditionQueryType.Exists:
                    stringBuilder.Append(PropertyConditionQueryType.Exists);
                    break;
                case PropertyConditionQueryType.NotExists:
                    stringBuilder.Append(PropertyConditionQueryType.NotExists);
                    break;
                case PropertyConditionQueryType.Value:
                    if (Comparison != null) Comparison.ToString(stringBuilder, ref index);
                    else stringBuilder.Append("NULL");
                    break;
            }
        }
    }

    /// <summary>
    /// Contains the condition on a property from an entity.
    /// Helper class for fluent syntax.
    /// </summary>
    [IgnoreInValidation]
    public class PropertyCondition<TReturn> : PropertyCondition, IQueryPropertyConditionBuilder<TReturn>
    {
        // Fields
        private readonly IChainedQueryConditionBuilder<TReturn> _parent;

        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionTextComparisonBuilder<string, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsString
        {
            get
            {
                var queryComparison = new QueryComparison<string, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(string));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionTextComparisonBuilder<Guid, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsGuid
        {
            get
            {
                var queryComparison = new QueryComparison<Guid, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(Guid?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<short, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsShort
        {
            get
            {
                var queryComparison = new QueryComparison<short, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(short?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<int, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsInt
        {
            get
            {
                var queryComparison = new QueryComparison<int, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(int?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<long, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsLong
        {
            get
            {
                var queryComparison = new QueryComparison<long, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(long?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<byte, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsByte
        {
            get
            {
                var queryComparison = new QueryComparison<byte, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(byte?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<bool, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsBool
        {
            get
            {
                var queryComparison = new QueryComparison<bool, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(bool?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<decimal, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsDecimal
        {
            get
            {
                var queryComparison = new QueryComparison<decimal, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(decimal?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<float, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsFloat
        {
            get
            {
                var queryComparison = new QueryComparison<float, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(float?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<double, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsDouble
        {
            get
            {
                var queryComparison = new QueryComparison<double, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(double?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<DateTime, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsDate
        {
            get
            {
                var queryComparison = new QueryComparison<DateTime, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(DateTime?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryConditionComparisonBuilder<TimeSpan, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsTimespan
        {
            get
            {
                var queryComparison = new QueryComparison<TimeSpan, TReturn>(_parent);
                Type = HiveMindHelper.Storage.GetStorageType(typeof(TimeSpan?));
                Comparison = queryComparison;
                return queryComparison;
            }
        }

        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IChainedQueryConditionBuilder<TReturn> Exists
        {
            get
            {
                QueryType = PropertyConditionQueryType.Exists;
                return _parent;
            }
        }
        /// <inheritdoc/>

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IChainedQueryConditionBuilder<TReturn> NotExists
        {
            get
            {
                QueryType = PropertyConditionQueryType.NotExists;
                return _parent;
            }
        }

        /// <inheritdoc cref="JobPropertyCondition"/>
        /// <param name="parent">The parent builder that created this instance</param>
        public PropertyCondition(IChainedQueryConditionBuilder<TReturn> parent)
        {
            _parent = parent.ValidateArgument(nameof(parent));
        }

        IQueryConditionTextComparisonBuilder<T, TReturn> IQueryPropertyConditionBuilder<TReturn>.AsEnum<T>()
        {
            var queryComparison = new QueryComparison<T, TReturn>(_parent);
            Type = HiveMindHelper.Storage.GetStorageType(typeof(T?));
            Comparison = queryComparison;
            return queryComparison;
        }
    }
}
