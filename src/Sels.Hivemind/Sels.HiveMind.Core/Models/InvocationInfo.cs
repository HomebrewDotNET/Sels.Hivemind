using Newtonsoft.Json.Linq;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Expression = System.Linq.Expressions.Expression;

namespace Sels.HiveMind
{
    /// <inheritdoc cref="IInvocationInfo"/>
    public class InvocationInfo : IInvocationInfo
    {
        // Fields
        /// <summary>
        /// The arguments for <see cref="MethodInfo"/> if there are any.
        /// </summary>
        protected readonly List<object> _arguments = new List<object>();

        // Properties
        /// <inheritdoc/>
        public Type Type { get; protected set; }
        /// <inheritdoc/>
        public MethodInfo MethodInfo { get; protected set; }
        /// <inheritdoc/>
        public IReadOnlyList<object> Arguments => _arguments;

        /// <inheritdoc cref="InvocationInfo"/>
        /// <param name="expression">Expression to parse the invocation data from</param>
        public InvocationInfo(Expression<Func<object>> expression)
        {
            expression.ValidateArgument(nameof(expression));

            // Parse method
            var methodCallExpression = expression.ExtractMethodCall();
            SetFromMethodCall(methodCallExpression, null);
        }

        /// <inheritdoc cref="InvocationInfo"/>
        /// <param name="expression">The instance to convert from</param>
        public InvocationInfo(InvocationStorageData data)
        {
            data.ValidateArgument(nameof(data));

            Type = data.Type;

            MethodInfo = Type.GetMethod(data.MethodName, data.GenericArguments.HasValue() ? data.GenericArguments.Count : 0, data.Arguments.HasValue() ? data.Arguments.Select(x => x.Type).ToArray() : Array.Empty<Type>());
            if (MethodInfo == null) throw new InvalidOperationException($"Cannot find method <{data.MethodName}> on type <{Type}>");
            if(MethodInfo.IsGenericMethodDefinition) MethodInfo = MethodInfo.MakeGenericMethod(data.GenericArguments.ToArray());

            if (data.Arguments.HasValue()) _arguments.AddRange(data.Arguments.Select(x => x.Value));
        }

        /// <summary>
        /// Ctor for derived classes.
        /// </summary>
        protected InvocationInfo()
        {
            
        }

        /// <summary>
        /// Sets the properties of the current instance based on <paramref name="methodCallExpression"/>.
        /// </summary>
        /// <param name="methodCallExpression">The expression to parse information from</param>
        /// <param name="instanceType">The instance type <paramref name="methodCallExpression"/> was taken from. Can be null if method is static</param>
        protected void SetFromMethodCall(MethodCallExpression methodCallExpression, Type instanceType)
        {
            Type = instanceType;
            MethodInfo = methodCallExpression.Method;

            if (MethodInfo.IsStatic)
            {
                Type = MethodInfo.DeclaringType;
            }
            else
            {
                if (Type == null) throw new ArgumentException($"Method <{MethodInfo.Name}> is not static but no instance type is defined");
                // Validate method is actually from type
                Type.ValidateArgumentAssignableTo(nameof(instanceType), MethodInfo.DeclaringType);
            }

            if (!Type.IsPublic) throw new InvalidOperationException($"Type must be public");
            if (!MethodInfo.IsPublic) throw new InvalidOperationException($"MethodInfo must be public");

            // Parse arguments
            if (methodCallExpression.Arguments.HasValue())
            {
                foreach (var (i, argument) in methodCallExpression.Arguments.Select((x, i) => (i, x)))
                {
                    if(!TryGetValue(argument, out var value))
                    {
                        throw new InvalidOperationException($"Could not extract instance from argument <{i}> for method <{MethodInfo.Name}>. Expression is <{argument}> of type <{argument.GetType().GetDisplayName()}> which is not supported");
                    }
                   
                    _arguments.Add(value);
                }
            }
        }

        private bool TryGetValue(Expression expression, out object constantValue)
        {
            constantValue = null;

            if (expression is ConstantExpression constantExpression)
            {
                constantValue = constantExpression.Value;
                return true;
            }
            else if (expression is MemberExpression memberExpression)
            {
                object declaringObject = memberExpression.Member.DeclaringType;
                if(memberExpression.Expression is ConstantExpression memberConstantExpression)
                {
                    declaringObject = memberConstantExpression.Value;
                }

                if(memberExpression.Member is FieldInfo field)
                {
                    constantValue = field.GetValue(declaringObject);
                    return true;
                }
                else if(memberExpression.Member is PropertyInfo property)
                {
                    constantValue = property.GetValue(declaringObject);
                    return true;
                }
            }
            else if(expression is UnaryExpression unaryExpression)
            {
                return TryGetValue(unaryExpression.Operand, out constantValue);
            }

            return false;
        }
    }

    /// <summary>
    /// <inheritdoc cref="IInvocationInfo"/>.
    /// Uses ctor to parse method from expressions.
    /// </summary>
    /// <typeparam name="T">The type to create the invocation for</typeparam>
    public class InvocationInfo<T> : InvocationInfo
    {
        /// <inheritdoc cref="InvocationInfo{T}"/>
        /// <param name="expression">Expression to parse the invocation data from</param>
        public InvocationInfo(Expression<Func<T, object>> expression)
        {
            expression.ValidateArgument(nameof(expression));
            Type = typeof(T);

            // Parse method
            var methodCallExpression = expression.ExtractMethodCall();
            SetFromMethodCall(methodCallExpression, typeof(T));
        }
    }
}
