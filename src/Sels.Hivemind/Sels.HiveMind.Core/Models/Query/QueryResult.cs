using Sels.HiveMind.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Query result that doesn't require any special diposing of the results.
    /// </summary>
    /// <typeparam name="T">The type returned from the query</typeparam>
    public class QueryResult<T> : IClientQueryResult<T>
    {
        /// <inheritdoc/>
        public IReadOnlyList<T> Results { get; }

        /// <inheritdoc cref="QueryResult{T}"/>
        /// <param name="results"><see cref="Results"/></param>
        public QueryResult(IEnumerable<T> results)
        {
            Results = Guard.IsNotNull(results).ToList();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
