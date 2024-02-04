using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Provides access to <see cref="ICalendar"/> with certain names.
    /// </summary>
    public interface ICalendarProvider
    {
        /// <summary>
        /// Gets a new instance of <see cref="ICalendar"/>with name <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the calendar to fetch</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new instance of <see cref="ICalendar"/>with name <paramref name="name"/></returns>
        public Task<IComponent<ICalendar>> GetCalendarAsync(string name, CancellationToken token = default);
    }
}
