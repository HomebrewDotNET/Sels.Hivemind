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
    public class MiddlewareStorageData : IMiddlewareInfo
    {
        /// <summary>
        /// The type name of the middleware to execute.
        /// </summary>
        public Type Type { get; set; }
        [JsonProperty(TypeNameHandling = TypeNameHandling.All)]
        /// <inheritdoc cref="IMiddlewareInfo.Context"/>
        public object Context { get; set; }
        /// <inheritdoc cref="IMiddlewareInfo.Priority"/>
        public uint? Priority { get; }

        /// <summary>
        /// Creates new instance from <paramref name="middlewareInfo"/>.
        /// </summary>
        /// <param name="middlewareInfo">The instance to convert from</param>
        public MiddlewareStorageData(IMiddlewareInfo middlewareInfo)
        {
            middlewareInfo.ValidateArgument(nameof(middlewareInfo));
            Type = middlewareInfo.Type;
            Context = middlewareInfo.Context;
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
