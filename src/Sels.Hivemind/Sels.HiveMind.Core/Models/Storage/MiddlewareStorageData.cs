using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Middleware on a component transformed into a format for storage.
    /// </summary>
    public class MiddlewareStorageData
    {
        /// <summary>
        /// The type name of the middleware to execute.
        /// </summary>
        public string TypeName { get; set; }
        /// <summary>
        /// The name of the type if <see cref="Context"/> is set.
        /// </summary>
        public string ContextTypeName { get; set; }
        /// <summary>
        /// The Optional serialized context for the middleware.
        /// </summary>
        public string Context { get; set; }
        /// <inheritdoc cref="IMiddlewareInfo.Priority"/>
        public byte? Priority { get; set; }

        /// <summary>
        /// Creates new instance from <paramref name="middlewareInfo"/>.
        /// </summary>
        /// <param name="middlewareInfo">The instance to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public MiddlewareStorageData(IMiddlewareInfo middlewareInfo, HiveMindOptions options, IMemoryCache cache = null)
        {
            middlewareInfo.ValidateArgument(nameof(middlewareInfo));
            options.ValidateArgument(nameof(options));
            TypeName = middlewareInfo.Type.AssemblyQualifiedName;
            
            if(middlewareInfo.Context != null)
            {
                ContextTypeName = middlewareInfo.Context.GetType().AssemblyQualifiedName;
                Context = HiveMindHelper.Storage.ConvertToStorageFormat(middlewareInfo.Context, options, cache);
            }

            Priority = middlewareInfo.Priority;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public MiddlewareStorageData()
        {
            
        }
    }
}
