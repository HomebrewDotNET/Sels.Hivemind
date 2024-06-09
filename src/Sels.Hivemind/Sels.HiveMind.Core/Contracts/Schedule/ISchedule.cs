using System;
using System.Collections.Generic;
using System.Text;
using Sels.Core.Extensions;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Interval;

namespace Sels.HiveMind.Schedule
{
    /// <summary>
    /// Contains the configuration on how to schedule a component.
    /// </summary>
    public interface ISchedule
    {
        /// <summary>
        /// Indicates if the schedule is currently empty. (nothing configured)
        /// </summary>
        public bool IsEmpty => !IntervalName.HasValue() && !InclusionCalendars.HasValue() && !ExclusionCalendars.HasValue();

        /// <summary>
        /// The name of the interval to use. Can be null if a calendar is defined.
        /// </summary>
        public string IntervalName { get; set; }
        /// <summary>
        /// The interval that will be used with <see cref="IInterval"/> with name <see cref="IntervalName"/>. Can be null if a calendar is defined.
        /// </summary>
        public object Interval { get; set; }

        /// <summary>
        /// Optional list of calendars used to determine if the component is allowed to run.
        /// When used with an interval, the component will only run if the generated interval is allowed by any calendars.
        /// If no interval is defined the next execution date will be determined by the first calendar that allows it.
        /// </summary>
        public IReadOnlyList<IScheduleCalendar> InclusionCalendars { get; set; }
        /// <summary>
        /// Optional list of calendars used to determine when the component is not allowed to run.
        /// When used with an interval, the component will not run if the generated interval is in range of any of the calendars.
        /// If no interval or inclusion calendars are defined the next execution date will be the first dat that is not in range of any of the exclusion calendars.
        /// </summary>
        public IReadOnlyList<IScheduleCalendar> ExclusionCalendars { get; set; }
    }

    /// <summary>
    /// Contains the configuration of a calendar to use with a component.
    /// </summary>
    public interface IScheduleCalendar
    {
        /// <summary>
        /// The name of the calendar to use. Mutually exclusive with <see cref="Calendar"/>.
        /// </summary>
        public string CalendarName { get; set; }
        /// <summary>
        /// The calendar to use. Must be serializable. Mutually exclusive with <see cref="CalendarName"/>.
        /// </summary>
        public ICalendar Calendar { get; set; }
    }
}
