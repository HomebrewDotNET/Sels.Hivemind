using System;
using System.Collections.Generic;
using Sels.HiveMind.Schedule;

using System.Text;

namespace Sels.HiveMind.Models.Storage.Schedule
{
    /// <summary>
    /// Component configuration on how to schedule it transformed into a format for storage.
    /// </summary>
    public class ScheduleStorageData
    {
        /// <inheritdoc cref="ISchedule.IntervalName"/>
        public string IntervalName { get; set; }
        /// <summary>
        /// The type of the interval that will be used with <see cref="IntervalName"/>.
        /// </summary>
        public string IntervalTypeName { get; set; }
        /// <summary>
        /// The serialized value of <see cref="IntervalStorageValue"/>.
        /// </summary>
        public string IntervalStorageValue { get; set; }

        /// <inheritdoc cref="ISchedule.InclusionCalendars"/>
        public IReadOnlyList<ScheduleCalendarStorageData> InclusionCalendars { get; set; }
        /// <inheritdoc cref="ISchedule.ExclusionCalendars"/>
        public IReadOnlyList<ScheduleCalendarStorageData> ExclusionCalendars { get; set; }
    }

    /// <summary>
    /// The configuration of a calendar to use with a schedule transformed into a format for storage.
    /// </summary>
    public class ScheduleCalendarStorageData
    {
        /// <inheritdoc cref="IScheduleCalendar.CalendarName"/>
        public string CalendarName { get; set; }
        /// <summary>
        /// The name of the type of the calendar to use if set.
        /// </summary>
        public string CalendarTypeName { get; set; }
        /// <summary>
        /// The serialized value of <see cref="CalendarStorageValue"/>.
        /// </summary>
        public string CalendarStorageValue { get; set; }
    }
}
