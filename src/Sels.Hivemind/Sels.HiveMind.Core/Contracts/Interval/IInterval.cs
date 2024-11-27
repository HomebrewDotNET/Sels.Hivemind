using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Interval
{
    /// <summary>
    /// Represents an interval that can be used to generate the next date based on a given date.
    /// </summary>
    public interface IInterval
    {
        /// <summary>
        /// Returns the next date after <paramref name="date"/> based on the interval defined in <paramref name="interval"/>.
        /// </summary>
        /// <param name="date">The date to generate the next date for</param>
        /// <param name="interval">The configured interval</param>
        /// <param name="cancellationToken">Optional token to cancel the request</param>
        /// <returns>The next date after <paramref name="date"/> based on <paramref name="interval"/></returns>
        Task<DateTime> GetNextDateAsync(DateTime date, object interval, CancellationToken cancellationToken = default);
    }
}
