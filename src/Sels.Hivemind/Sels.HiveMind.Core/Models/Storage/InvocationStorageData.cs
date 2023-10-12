using Newtonsoft.Json;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Invocation data about how to execute a component transformed into a format for storage.
    /// </summary>
    public class InvocationStorageData
    {
        /// <summary>
        /// The type name of the instance (or static type) to execute.
        /// </summary>
        public Type Type { get; set; }
        /// <summary>
        /// The return type of <see cref="MethodName"/>.
        /// </summary>
        public Type ReturnType { get; set; }
        /// <summary>
        /// The name of the method to invoke on <see cref="Type"/>.
        /// </summary>
        public string MethodName { get; set; }
        /// <summary>
        /// List with the names of the generic types in case <see cref="MethodName"/> is generic.
        /// </summary>
        public List<Type> GenericArguments { get; set; }
        /// <summary>
        /// Optional arguments for the method.
        /// </summary>
        public List<InvocationArgumentStorageData> Arguments { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="invocationInfo"/>.
        /// </summary>
        /// <param name="invocationInfo">The instance to convert from</param>
        public InvocationStorageData(IInvocationInfo invocationInfo)
        {
            invocationInfo.ValidateArgument(nameof(invocationInfo));
            Type = invocationInfo.Type;
            ReturnType = invocationInfo.MethodInfo.ReturnType;
            MethodName = invocationInfo.MethodInfo.Name;

            if (invocationInfo.MethodInfo.IsGenericMethod)
            {
                var genericArguments = invocationInfo.MethodInfo.GetGenericArguments();

                GenericArguments = genericArguments.ToList();
            }

            var parameters = invocationInfo.MethodInfo.GetParameters();

            for(int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var argument = invocationInfo.Arguments[i];

                Arguments ??= new List<InvocationArgumentStorageData>();
                Arguments.Add(new InvocationArgumentStorageData() { Type = parameter.ParameterType, Value = argument });
            }
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public InvocationStorageData()
        {
            
        }
    }
    /// <summary>
    /// An argument for a method transformed into a format for storage.
    /// </summary>
    public class InvocationArgumentStorageData
    {
        /// <summary>
        /// The type name of the argument.
        /// </summary>
        public Type Type { get; set; }
        [JsonPropertyAttribute(TypeNameHandling = TypeNameHandling.All)]
        /// <summary>
        /// Optional value of the argument.
        /// </summary>
        public object Value { get; set; }

        public InvocationArgumentStorageData()
        {
            
        }
    }
}
