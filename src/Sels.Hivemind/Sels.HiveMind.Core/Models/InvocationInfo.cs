using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
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
        private readonly object _lock = new object();
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // State
        private Type _type;
        private List<object> _arguments;
        private MethodInfo _method;
        private InvocationStorageData _data;

        // Properties
        /// <inheritdoc/>
        public Type Type { 
            get { 
                lock (_lock)
                {
                    if (_type == null && _data != null && _data.TypeName != null)
                    {
                        _type = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.TypeName, typeof(Type), _options, _cache).CastTo<Type>();
                    }

                    return _type;
                }
            } 
            protected set {
                lock (_lock)
                {
                    _type = value;
                    _data ??= new InvocationStorageData();
                    _data.TypeName = value != null ? HiveMindHelper.Storage.ConvertToStorageFormat(value, _options, _cache) : null;
                }
            } 
        }
        /// <inheritdoc/>
        public MethodInfo MethodInfo
        {
            get
            {
                lock (_lock)
                {
                    if (_method == null && _data != null && _data.MethodName != null && Type != null)
                    {
                        _method = Type.FindMethod(_data.MethodName, _data.GenericArguments.HasValue() ? _data.GenericArguments.Select(x => {
                            return HiveMindHelper.Storage.ConvertFromStorageFormat(x, typeof(Type), _options, _cache).CastTo<Type>();
                        }).ToArray() : Array.Empty<Type>(), _data.Arguments.HasValue() ? _data.Arguments.Select(x => {
                            return HiveMindHelper.Storage.ConvertFromStorageFormat(x.TypeName, typeof(Type), _options, _cache).CastTo<Type>();
                        }).ToArray() : Array.Empty<Type>(), true);

                        if( _method != null && _method.IsGenericMethodDefinition && _data.GenericArguments.HasValue())
                        {
                            _method = _method.MakeGenericMethod(_data.GenericArguments.Select(x =>
                            {
                                return HiveMindHelper.Storage.ConvertFromStorageFormat(x, typeof(Type), _options, _cache).CastTo<Type>();
                            }).ToArray());
                        }
                    }

                    return _method;
                }
            }
            protected set
            {
                lock (_lock)
                {
                    if(value != null)
                    {
                        SetFromMethod(value);
                        _data ??= new InvocationStorageData();
                        _data.MethodName = value.Name;
                        _data.GenericArguments = value.IsGenericMethod && !value.IsGenericMethodDefinition ? value.GetGenericArguments().Select(x => HiveMindHelper.Storage.ConvertToStorageFormat(x, _options, _cache)).ToList() : new List<string>();
                        _data.Arguments = value.GetParameters().Select(x => new InvocationArgumentStorageData() { TypeName = HiveMindHelper.Storage.ConvertToStorageFormat(x.ParameterType, _options, _cache) }).ToList();
                    }
                    else
                    {
                        _type = null;
                        _method = null;
                        _data ??= new InvocationStorageData();
                        _data.MethodName = null;
                        _data.GenericArguments = null;
                        _data.Arguments = null;
                    }
                }
            }
        }
        /// <inheritdoc/>
        public IReadOnlyList<object> Arguments
        {
            get
            {
                lock (_lock)
                {
                    if (_arguments == null && _data != null && _data.Arguments.HasValue())
                    {
                        _arguments = _data.Arguments.Select(x =>
                        {
                            var type = HiveMindHelper.Storage.ConvertFromStorageFormat(x.TypeName, typeof(Type), _options, _cache).CastTo<Type>();
                            return HiveMindHelper.Storage.ConvertFromStorageFormat(x.Value, type, _options, _cache);
                        }).ToList();
                    }

                    return _arguments;
                }
            }
            protected set
            {
                lock (_lock)
                {
                    _arguments = value?.ToList();
                    _data ??= new InvocationStorageData();

                    if(_data.Arguments == null)
                    {
                        _data.Arguments = _arguments?.Select(x =>
                        {
                            var type = HiveMindHelper.Storage.ConvertToStorageFormat(x?.GetType() ?? typeof(object), _options, _cache);
                            var value = x != null ? HiveMindHelper.Storage.ConvertToStorageFormat(x, _options, _cache) : null;

                            return new InvocationArgumentStorageData() { TypeName = type, Value = value };
                        }).ToList();
                    }
                    else if (_arguments != null)
                    {
                        if(_arguments.Count != _data.Arguments.Count) throw new InvalidCastException($"Method <{_data.MethodName}> expects <{_data.Arguments.Count}> arguments but only <{_arguments.Count}> were provided");

                        _data.Arguments.Execute((i, x) =>
                        {
                            var argument = _arguments.Skip(i).FirstOrDefault();
                            var value = argument != null ? HiveMindHelper.Storage.ConvertToStorageFormat(argument, _options, _cache) : null;
                            x.Value = value;
                        });
                    }
                    else
                    {
                        _data.Arguments.Execute(x => x.Value = null);
                    }
                }
            }
        }

        /// <summary>
        /// The current instance converted into it's storage equivalent.
        /// </summary>
        public InvocationStorageData StorageData
        {
            get
            {
                lock (_lock)
                {
                    if(_data == null)
                    {
                        _data = new InvocationStorageData();
                    }
                    return _data;
                }
            }
        }

        /// <inheritdoc cref="InvocationInfo"/>
        /// <param name="expression">Expression to parse the invocation data from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public InvocationInfo(Expression<Func<object>> expression, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            expression.ValidateArgument(nameof(expression));

            // Parse method
            var methodCallExpression = expression.ExtractMethodCall();
            SetFromMethodCall(methodCallExpression);
        }

        /// <inheritdoc cref="InvocationInfo"/>
        /// <param name="data">The data to parse from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public InvocationInfo(InvocationStorageData data, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            data.ValidateArgument(nameof(data));

            _data = data;
        }

        /// <summary>
        /// Ctor for derived classes.
        /// </summary>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        protected InvocationInfo(HiveMindOptions options, IMemoryCache cache = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }

        /// <summary>
        /// Sets the properties of the current instance based on <paramref name="methodCallExpression"/>.
        /// </summary>
        /// <param name="methodCallExpression">The expression to parse information from</param>
        /// <param name="instanceType">The instance type <paramref name="methodCallExpression"/> was taken from. Can be null if method is static</param>
        protected void SetFromMethodCall(MethodCallExpression methodCallExpression)
        {
            methodCallExpression.ValidateArgument(nameof(methodCallExpression));

            MethodInfo = methodCallExpression.Method;

            // Parse arguments
            if (methodCallExpression.Arguments.HasValue())
            {
                var arguments = new List<object>();
                foreach (var (i, argument) in methodCallExpression.Arguments.Select((x, i) => (i, x)))
                {
                    if(!TryGetValue(argument, out var value))
                    {
                        throw new InvalidOperationException($"Could not extract instance from argument <{i}> for method <{MethodInfo.Name}>. Expression is <{argument}> of type <{argument.GetType().GetDisplayName()}> which is not supported");
                    }

                    if(value != null && HiveMindHelper.Storage.IsSpecialArgumentType(value.GetType()))
                    {
                        value = null;
                    }

                    arguments ??= new List<object>();
                    arguments.Add(value);
                }

                Arguments = arguments;
            }
        }

        /// <summary>
        /// Sets the properties of the current instance based on <paramref name="method"/>.
        /// </summary>
        /// <param name="method">The method to parse information from</param>
        /// <param name="instanceType">The instance type <paramref name="method"/> was taken from. Can be null if method is static</param>
        private void SetFromMethod(MethodInfo method)
        {
            Type = method.ReflectedType;

            _method = method;

            if (MethodInfo.IsStatic)
            {
                Type = MethodInfo.DeclaringType;
            }

            if (!Type.IsPublic) throw new InvalidOperationException($"Type must be public");
            if (!MethodInfo.IsPublic) throw new InvalidOperationException($"MethodInfo must be public");
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
            else
            {
                try
                {
                    constantValue = Expression.Lambda(expression).Compile().DynamicInvoke();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (_data != null) return $"Method <{_data.MethodName}> from type <{_data.TypeName}> with <{(_data?.GenericArguments?.Count ?? 0)}> generic arguments and <{(_data?.Arguments?.Count ?? 0)}> method parameters";
            return base.ToString();
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
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public InvocationInfo(Expression<Func<T, object>> expression, HiveMindOptions options, IMemoryCache cache = null) : base(options, cache)
        {
            expression.ValidateArgument(nameof(expression));
            Type = typeof(T);

            // Parse method
            var methodCallExpression = expression.ExtractMethodCall();
            SetFromMethodCall(methodCallExpression);
        }
    }
}
