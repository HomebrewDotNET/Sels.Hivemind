using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Middleware on a component transformed into a format for storage.
    /// </summary>
    public interface IMiddlewareStorageData
    {
        /// <summary>
        /// The type name of the middleware to execute.
        /// </summary>
        public string TypeName { get; }
        /// <summary>
        /// The name of the type if <see cref="Context"/> is set.
        /// </summary>
        public string ContextTypeName { get; }
        /// <summary>
        /// The optional serialized context for the middleware.
        /// </summary>
        public string Context { get; }
        /// <inheritdoc cref="IMiddlewareInfo.Priority"/>
        public byte? Priority { get; }
    }
}
