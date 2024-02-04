using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Represents a range of dates that is used to check if a date is within the range.
    /// </summary>
    public interface ICalendar
    {
        /// <summary>
        /// Checks if the provided date is within the range of the calendar.
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if <paramref name="date"/> is within the range of the calendar, otherwise false</returns>
        public Task<bool> IsInRange(DateTime date);
        /// <summary>
        /// Returns the next date that is within the range of the calendar after <paramref name="date"/>.
        /// </summary>
        /// <param name="date">The date boundry after which the next date can be in</param>
        /// <returns>The next date after <paramref name="date"/> that is in range of the current calendar or <paramref name="date"/> if it's already in range</returns>
        public Task<DateTime> GetNextInRange(DateTime date);
        /// <summary>
        /// Returns the next date after <paramref name="date"/> that is just outside the range of the calendar.
        /// </summary>
        /// <param name="date">The date boundry after which the next date can be in</param>
        /// <returns>The next date after <paramref name="date"/> that is just outside the range of the calendar or <paramref name="date"/> if it's already outside of the range</returns>
        public Task<DateTime> GetNextOutsideOfRange(DateTime date);
    }
}
