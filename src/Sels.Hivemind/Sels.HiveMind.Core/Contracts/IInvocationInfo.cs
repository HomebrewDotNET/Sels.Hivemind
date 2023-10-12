using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains the invocation data on how to execute a component.
    /// </summary>
    public interface IInvocationInfo
    {
        /// <summary>
        /// The type of the instance (or static type) to execute.
        /// </summary>
        public Type Type { get; }
        /// <summary>
        /// The method on <see cref="Type"/> to call to execute the method.
        /// </summary>
        public MethodInfo MethodInfo { get; }
        /// <summary>
        /// Optional arguments for <see cref="MethodInfo"/>. 
        /// </summary>
        public IReadOnlyList<object> Arguments { get; }
    }
}
