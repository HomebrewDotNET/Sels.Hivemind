using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.HiveMind.Calendar;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Schedule
{
    /// <inheritdoc cref="IScheduleBuilder"/>
    public class ScheduleBuilder : IScheduleBuilder
    {
        // Fields
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // State
        private string _intervalName;
        private object _interval;
        private List<ScheduleCalendarInfo> _inclusionCalendars;
        private List<ScheduleCalendarInfo> _exclusionCalendars;

        // Properties
        public ScheduleInfo ScheduleInfo => new ScheduleInfo(_intervalName, _interval, _inclusionCalendars, _exclusionCalendars, _options, _cache);

        /// <inheritdoc cref="ScheduleBuilder"/>
        /// <param name="configurator">Delegate used to configure the current instance</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ScheduleBuilder(Action<IScheduleBuilder> configurator, HiveMindOptions options, IMemoryCache cache = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
            configurator.ValidateArgument(nameof(configurator))(this);
        }
        /// <inheritdoc/>
        public IScheduleBuilder WithInterval(string intervalName, object interval)
        {
            _intervalName = intervalName.ValidateArgumentNotNullOrWhitespace(nameof(intervalName));
            _interval = interval.ValidateArgument(nameof(interval));
            return this;
        }
        /// <inheritdoc/>
        public IScheduleBuilder WithCalendar(string calendarName, bool isForExcluding = false)
        {
            calendarName.ValidateArgumentNotNullOrWhitespace(nameof(calendarName));
            if (isForExcluding)
            {
                _exclusionCalendars ??= new List<ScheduleCalendarInfo>();
                _exclusionCalendars.Add(new ScheduleCalendarInfo(calendarName, _options, _cache));
            }
            else
            {
                _inclusionCalendars ??= new List<ScheduleCalendarInfo>();
                _inclusionCalendars.Add(new ScheduleCalendarInfo(calendarName, _options, _cache));
            }
            return this;
        }
        /// <inheritdoc/>
        public IScheduleBuilder WithCalendar(ICalendar calendar, bool isForExcluding = false)
        {
            calendar.ValidateArgument(nameof(calendar));

            if (isForExcluding)
            {
                _exclusionCalendars ??= new List<ScheduleCalendarInfo>();
                _exclusionCalendars.Add(new ScheduleCalendarInfo(calendar, _options, _cache));
            }
            else
            {
                _inclusionCalendars ??= new List<ScheduleCalendarInfo>();
                _inclusionCalendars.Add(new ScheduleCalendarInfo(calendar, _options, _cache));
            }
            return this;
        }
    }
}
