using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Middleware that is to be executed in the execution chain of a component.
    /// </summary>
    public interface IMiddlewareInfo
    {
        /// <summary>
        /// The type of the middleware to execute.
        /// </summary>
        public Type Type { get; }
        /// <summary>
        /// Optional context for the middleware.
        /// </summary>
        public object Context { get; }
        /// <summary>
        /// Optional priority of the middleware to determine the execution order. Lower priority means executed first, null means executed last in the chain.
        /// </summary>
        public byte? Priority { get; }
    }
}
