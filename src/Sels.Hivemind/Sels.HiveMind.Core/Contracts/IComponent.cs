using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// A component that can be used within the scope defined by the dispose scope.
    /// </summary>
    /// <typeparam name="T">The type of the component</typeparam>
    public interface IComponent<T> : IAsyncDisposable where T : class
    {
        /// <summary>
        /// The component that can be used within the scope.
        /// </summary>
        public T Component { get; }
    }
}
