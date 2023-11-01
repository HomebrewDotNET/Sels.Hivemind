using System;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Contains the results returned by a client query.
    /// </summary>
    /// <typeparam name="T">The type of result returned</typeparam>
    public interface IClientQueryResult<T> : IAsyncDisposable
    {
        /// <summary>
        /// The total amount of results that matched the query condition.
        /// </summary>
        public long Total { get; }
        /// <summary>
        /// The results returned by the query.
        /// </summary>
        public T[] Results { get; }
    }
}
