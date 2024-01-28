using Microsoft.Extensions.Caching.Memory;
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
        public string TypeName { get; set; }
        /// <summary>
        /// The return type of <see cref="MethodName"/>.
        /// </summary>
        public string ReturnTypeName { get; set; }
        /// <summary>
        /// The name of the method to invoke on <see cref="TypeName"/>.
        /// </summary>
        public string MethodName { get; set; }
        /// <summary>
        /// List with the names of the generic types in case <see cref="MethodName"/> is generic.
        /// </summary>
        public List<string> GenericArguments { get; set; }
        /// <summary>
        /// Optional arguments for the method.
        /// </summary>
        public List<InvocationArgumentStorageData> Arguments { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="invocationInfo"/>.
        /// </summary>
        /// <param name="invocationInfo">The instance to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public InvocationStorageData(IInvocationInfo invocationInfo, HiveMindOptions options, IMemoryCache cache = null)
        {
            invocationInfo.ValidateArgument(nameof(invocationInfo));
            options.ValidateArgument(nameof(options));

            TypeName = invocationInfo.Type.AssemblyQualifiedName;
            ReturnTypeName = invocationInfo.MethodInfo.ReturnType.AssemblyQualifiedName;
            MethodName = invocationInfo.MethodInfo.Name;

            if (invocationInfo.MethodInfo.IsGenericMethod)
            {
                var genericArguments = invocationInfo.MethodInfo.GetGenericArguments();

                GenericArguments = genericArguments.Select(x => x.AssemblyQualifiedName).ToList();
            }

            var parameters = invocationInfo.MethodInfo.GetParameters();

            for(int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var argument = invocationInfo.Arguments[i];

                Arguments ??= new List<InvocationArgumentStorageData>();
                Arguments.Add(new InvocationArgumentStorageData() { TypeName = parameter.ParameterType.AssemblyQualifiedName, Value = argument != null ?  HiveMindHelper.Storage.ConvertToStorageFormat(argument, options, cache) : null});
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
        public string TypeName { get; set; }
        /// <summary>
        /// The Optional serialized value.
        /// </summary>
        public string Value { get; set; }

        public InvocationArgumentStorageData()
        {
            
        }
    }
}
