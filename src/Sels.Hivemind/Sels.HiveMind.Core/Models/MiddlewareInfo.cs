using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <inheritdoc cref="IMiddlewareInfo"/>
    public class MiddlewareInfo : IMiddlewareInfo
    {
        /// <inheritdoc/>
        public Type Type { get; private set; }
        /// <inheritdoc/>
        public object Context { get; private set; }
        /// <inheritdoc/>
        public uint? Priority { get; private set; }

        /// <inheritdoc cref="Middleware"/>
        /// <param name="type"><inheritdoc cref="Type"/></param>
        /// <param name="context"><inheritdoc cref="Context"/></param>
        /// <param name="priority"><inheritdoc cref="Priority"/></param>
        public MiddlewareInfo(Type type, object context, uint? priority)
        {
            Type = type.ValidateArgument(nameof(type));
            Context = context;
            Priority = priority;
        }
    }
}
