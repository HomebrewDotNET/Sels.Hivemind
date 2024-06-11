using System;
using System.Collections.Generic;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Contains the results returned by a client query.
    /// </summary>
    /// <typeparam name="T">The type of result returned</typeparam>
    public interface IClientQueryResult<out T> : IAsyncDisposable
    {
        /// <summary>
        /// The results returned by the query.
        /// </summary>
        public IReadOnlyList<T> Results { get; }
    }
}
