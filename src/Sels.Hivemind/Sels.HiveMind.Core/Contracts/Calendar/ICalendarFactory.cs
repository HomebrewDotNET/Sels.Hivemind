using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Factory that is able to create instances of <see cref="ICalendar"/> with a certain name.
    /// </summary>
    public interface ICalendarFactory
    {
        /// <summary>
        /// The name of the calendar that is created by this factory.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates an instance of <see cref="ICalendar"/> with name <see cref="Name"/>.
        /// </summary>
        /// <param name="serviceProvider">Service provider that can be used to resolve dependencies. Scope is managed by caller</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>A new instance of <see cref="ICalendar"/> with name <see cref="Name"/></returns>
        Task<ICalendar> CreateCalendarAsync(IServiceProvider serviceProvider, CancellationToken token = default);
    }
}
