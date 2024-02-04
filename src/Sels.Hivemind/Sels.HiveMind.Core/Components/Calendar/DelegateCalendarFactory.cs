using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Factory that is able to create instances of <see cref="ICalendar"/> with a certain name by delegating the construction to a delegate.
    /// </summary>
    public class DelegateCalendarFactory : ICalendarFactory
    {
        // Fields
        private readonly Func<IServiceProvider, CancellationToken, Task<ICalendar>> _calendarFactory;

        // Properties
        ///<inheritdoc/>
        public string Name { get;}

        /// <inheritdoc cref="DelegateCalendarFactory"/>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="calendarFactory">Delegate that will be called to create the calendar. Matches methos signiture of <see cref="CreateCalendarAsync(IServiceProvider, CancellationToken)"/></param>
        public DelegateCalendarFactory(string name, Func<IServiceProvider, CancellationToken, Task<ICalendar>> calendarFactory)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            _calendarFactory = calendarFactory.ValidateArgument(nameof(calendarFactory));
        }

        ///<inheritdoc/>
        public Task<ICalendar> CreateCalendarAsync(IServiceProvider serviceProvider, CancellationToken token = default) => _calendarFactory(serviceProvider, token);
    }
}
