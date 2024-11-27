using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Client;
using Sels.HiveMind.Client.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// Builder for creating a comparison on a property.
    /// </summary>
    /// <typeparam name="TReturn">The type to return for the fluent syntax</typeparam>
    public interface IQueryPropertyBuilder<TReturn>
    {
        /// <summary>
        /// Adds a condition on the property of an entity.
        /// </summary>
        /// <param name="name">The name of the property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryPropertyConditionBuilder<TReturn> Property(string name);
        /// <summary>
        /// Adds a condition on the property of an entity where the expected type of the property is <typeparamref name="T"/>.
        /// </summary>
        /// <param name="name">The name of the property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T, TReturn> Property<T>(string name)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            var actualType = typeof(T).GetActualType();

            switch (actualType)
            {
                case Type type when type == typeof(string):
                    return Property(name).AsString.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(Guid):
                    return Property(name).AsGuid.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(TimeSpan):
                    return Property(name).AsTimespan.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(short):
                    return Property(name).AsShort.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(int):
                    return Property(name).AsInt.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(long):
                    return Property(name).AsLong.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(byte):
                    return Property(name).AsByte.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(bool):
                    return Property(name).AsBool.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(decimal):
                    return Property(name).AsDecimal.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(float):
                    return Property(name).AsFloat.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(double):
                    return Property(name).AsDouble.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                case Type type when type == typeof(DateTime):
                    return Property(name).AsDate.CastTo<IQueryConditionTextComparisonBuilder<T, TReturn>>();
                default:
                    throw new NotSupportedException($"Type {actualType} is not a queryable type. If an enum was used use the EnumProperty<T>(string name)");
            }
        }

        /// <summary>
        /// Adds a condition on the property of an entity where the expected type of the property is <see cref="string"/>.
        /// </summary>
        /// <param name="name">The name of the property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<string, TReturn> TextProperty(string name) => Property(name).AsString;

        /// <summary>
        /// Adds a condition on the property of an entity where the expected type of the property is enum <typeparamref name="T"/>.
        /// </summary>
        /// <param name="name">The name of the property to add a condition on</param>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionTextComparisonBuilder<T, TReturn> EnumProperty<T>(string name) where T : struct, Enum => Property(name).AsEnum<T>();
    }
}
