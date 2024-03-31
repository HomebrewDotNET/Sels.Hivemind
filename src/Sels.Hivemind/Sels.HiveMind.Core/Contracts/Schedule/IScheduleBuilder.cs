using System;
using Sels.HiveMind.Calendar;

namespace Sels.HiveMind.Schedule
{
    /// <summary>
    /// Builder for configuring a schedule.
    /// </summary>
    public interface IScheduleBuilder
    {
        /// <summary>
        /// The interval to use to schedule the recurring job.
        /// </summary>
        /// <param name="intervalName"><inheritdoc cref="ISchedule.IntervalName"/></param>
        /// <param name="interval"><inheritdoc cref="ISchedule.Interval"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder WithInterval(string intervalName, object interval);
        /// <summary>
        /// Adds a calendar to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendarName"><inheritdoc cref="IScheduleCalendar.CalendarName"/></param>
        /// <param name="isForExcluding">True if the calendar must be added to <see cref="ISchedule.ExclusionCalendars"/>, false to add to <see cref="ISchedule.InclusionCalendars"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder WithCalendar(string calendarName, bool isForExcluding = false);
        /// <summary>
        /// Adds a calendar to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendar"><inheritdoc cref="IScheduleCalendar.Calendar"/></param>
        /// <param name="isForExcluding">True if the calendar must be added to <see cref="ISchedule.ExclusionCalendars"/>, false to add to <see cref="ISchedule.InclusionCalendars"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder WithCalendar(ICalendar calendar, bool isForExcluding = false);

        // Overloads
        /// <summary>
        /// Schedule the recurring job to run every <paramref name="interval"/>.
        /// </summary>
        /// <param name="interval">The interval to use</param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder RunEvery(TimeSpan interval) => WithInterval(HiveMindConstants.Intervals.TimeType, interval);

        /// <summary>
        /// Adds a calendar to <see cref="ISchedule.InclusionCalendars"/> to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendarName"><inheritdoc cref="IScheduleCalendar.CalendarName"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder OnlyDuring(string calendarName) => WithCalendar(calendarName, true);
        /// <summary>
        /// Adds a calendar to <see cref="ISchedule.InclusionCalendars"/> to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendar"><inheritdoc cref="IScheduleCalendar.Calendar"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder OnlyDuring(ICalendar calendar) => WithCalendar(calendar, true);
        /// <summary>
        /// Adds a calendar to <see cref="ISchedule.InclusionCalendars"/> to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendar">The built-in calendar to use</param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder OnlyDuring(Calendars calendar) => WithCalendar(calendar.ToString(), true);
        /// <summary>
        /// Adds a calendar to <see cref="ISchedule.ExclusionCalendars"/> to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendarName"><inheritdoc cref="IScheduleCalendar.CalendarName"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder NotDuring(string calendarName) => WithCalendar(calendarName, false);
        /// <summary>
        /// Adds a calendar to <see cref="ISchedule.ExclusionCalendars"/> to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendar"><inheritdoc cref="IScheduleCalendar.Calendar"/></param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder NotDuring(ICalendar calendar) => WithCalendar(calendar, false);
        /// <summary>
        /// Adds a calendar to <see cref="ISchedule.ExclusionCalendars"/> to use to schedule the recurring job.
        /// Method can be called multiple times to add multiple calendars.
        /// </summary>
        /// <param name="calendar">The built-in calendar to use</param>
        /// <returns>Current builder for method chaining</returns>
        IScheduleBuilder NotDuring(Calendars calendar) => WithCalendar(calendar.ToString(), false);
    }
}
