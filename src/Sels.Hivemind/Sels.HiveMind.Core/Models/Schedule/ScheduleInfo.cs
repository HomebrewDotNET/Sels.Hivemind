using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Models.Storage.Schedule;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Schedule
{
    /// <inheritdoc cref="ISchedule"/>
    public class ScheduleInfo : ISchedule
    {
        // Fields
        private readonly object _lock = new object();
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // State
        private string _intervalName;
        private object _interval;
        private IReadOnlyList<IScheduleCalendar> _inclusionCalendars;
        private IReadOnlyList<IScheduleCalendar> _exclusionCalendars;
        private ScheduleStorageData _data;

        // Properties
        /// <inheritdoc/>
        public string IntervalName
        {
            get
            {
                lock (_lock)
                {
                    if (_intervalName == null && _data != null)
                    {
                        _intervalName = _data.IntervalName;
                    }
                }
                return _intervalName;
            }
            set
            {
                lock (_lock)
                {
                    _intervalName = value;

                    if (value != null)
                    {
                        _data ??= new ScheduleStorageData();
                        _data.IntervalName = value;
                    }
                }
            }
        }
        /// <inheritdoc/>
        public object Interval
        {
            get
            {
                lock (_lock)
                {
                    if (_interval == null && _data != null && _data.IntervalTypeName != null && _data.IntervalStorageValue != null)
                    {
                        var type = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.IntervalTypeName, typeof(Type), _options, _cache).CastTo<Type>();
                        _interval = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.IntervalStorageValue, type, _options, _cache);
                    }
                }
                return _interval;
            }
            set
            {
                lock (_lock)
                {
                    _interval = value;

                    if (value != null)
                    {
                        _data ??= new ScheduleStorageData();
                        _data.IntervalTypeName = HiveMindHelper.Storage.ConvertToStorageFormat(value.GetType(), _options, _cache);
                        _data.IntervalStorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(value, _options, _cache);
                    }
                }
            }
        }
        /// <inheritdoc/>
        public IReadOnlyList<IScheduleCalendar> InclusionCalendars
        {
            get
            {
                lock (_lock)
                {
                    if (_inclusionCalendars == null && _data != null && _data.InclusionCalendars != null)
                    {
                        _inclusionCalendars = _data.InclusionCalendars.Select(x => new ScheduleCalendarInfo(x, _options, _cache)).ToList();
                    }
                }
                return _inclusionCalendars;
            }
            set
            {
                lock (_lock)
                {
                    _inclusionCalendars = value;

                    if (value != null)
                    {
                        _data ??= new ScheduleStorageData();
                        _data.InclusionCalendars = value.Select(x => x.CalendarName != null ? new ScheduleCalendarInfo(x.CalendarName, _options, _cache) : new ScheduleCalendarInfo(x.Calendar, _options, _cache)).Select(x => x.StorageData).ToList();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<IScheduleCalendar> ExclusionCalendars
        {
            get
            {
                lock (_lock)
                {
                    if (_exclusionCalendars == null && _data != null && _data.ExclusionCalendars != null)
                    {
                        _exclusionCalendars = _data.ExclusionCalendars.Select(x => new ScheduleCalendarInfo(x, _options, _cache)).ToList();
                    }
                }
                return _exclusionCalendars;
            }
            set
            {
                lock (_lock)
                {
                    _exclusionCalendars = value;

                    if (value != null)
                    {
                        _data ??= new ScheduleStorageData();
                        _data.ExclusionCalendars = value.Select(x => x.CalendarName != null ? new ScheduleCalendarInfo(x.CalendarName, _options, _cache) : new ScheduleCalendarInfo(x.Calendar, _options, _cache)).Select(x => x.StorageData).ToList();
                    }
                }
            }
        }
        /// <summary>
        /// The current instance tranformed into a storage format equivalent.
        /// </summary>
        public ScheduleStorageData StorageData
        {
            get
            {
                lock (_lock)
                {
                    if (_data == null)
                    {
                        _data = new ScheduleStorageData();
                    }
                    return _data;
                }
            }
        }

        /// <inheritdoc cref="ScheduleInfo"/>
        /// <param name="intervalName"><inheritdoc cref="IntervalName"/></param>
        /// <param name="interval"><inheritdoc cref="Interval"/></param>
        /// <param name="inclusionCalendars"><inheritdoc cref="InclusionCalendars"/></param>
        /// <param name="exclusionCalendars"><inheritdoc cref="ExclusionCalendars"/></param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ScheduleInfo(string intervalName, object interval, IReadOnlyList<IScheduleCalendar> inclusionCalendars, IReadOnlyList<IScheduleCalendar> exclusionCalendars, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            IntervalName = intervalName;
            Interval = interval;
            InclusionCalendars = inclusionCalendars;
            ExclusionCalendars = exclusionCalendars;
        }

        /// <inheritdoc cref="ScheduleInfo"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ScheduleInfo(ScheduleStorageData data, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            _data = data;
        }

        /// <summary>
        /// Ctor for derived classes.
        /// </summary>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        protected ScheduleInfo(HiveMindOptions options, IMemoryCache cache = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }
    }

    /// <inheritdoc cref="IScheduleCalendar"/>
    public class ScheduleCalendarInfo : IScheduleCalendar
    {
        // Fields
        private readonly object _lock = new object();
        private readonly HiveMindOptions _options;
        private readonly IMemoryCache _cache;

        // State
        private string _calendarName;
        private ICalendar _calendar;
        private ScheduleCalendarStorageData _data;

        // Properties
        /// <inheritdoc/>
        public string CalendarName
        {
            get
            {
                lock (_lock)
                {
                    if (_calendarName == null && _data != null)
                    {
                        _calendarName = _data.CalendarName;
                    }
                }
                return _calendarName;
            }
            set
            {
                lock (_lock)
                {
                    _calendarName = value;

                    if (value != null)
                    {
                        _data ??= new ScheduleCalendarStorageData();
                        _data.CalendarName = value;
                    }
                }
            }
        }
        /// <inheritdoc/>
        public ICalendar Calendar
        {
            get
            {
                lock (_lock)
                {
                    if (_calendar == null && _data != null && _data.CalendarTypeName != null && _data.CalendarStorageValue != null)
                    {
                        var type = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.CalendarTypeName, typeof(Type), _options, _cache).CastTo<Type>();
                        _calendar = HiveMindHelper.Storage.ConvertFromStorageFormat(_data.CalendarStorageValue, type, _options, _cache).CastTo<ICalendar>();
                    }
                }
                return _calendar;
            }
            set
            {
                lock (_lock)
                {
                    _calendar = value;

                    if (value != null)
                    {
                        _data ??= new ScheduleCalendarStorageData();
                        _data.CalendarTypeName = HiveMindHelper.Storage.ConvertToStorageFormat(value.GetType(), _options, _cache);
                        _data.CalendarStorageValue = HiveMindHelper.Storage.ConvertToStorageFormat(value, _options, _cache);
                    }
                }
            }
        }

        /// <summary>
        /// The current instance tranformed into a storage format equivalent.
        /// </summary>
        public ScheduleCalendarStorageData StorageData
        {
            get
            {
                lock (_lock)
                {
                    if (_data == null)
                    {
                        _data = new ScheduleCalendarStorageData();
                    }
                    return _data;
                }
            }
        }

        /// <inheritdoc cref="ScheduleCalendarInfo"/>
        /// <param name="calendarName"><inheritdoc cref="CalendarName"/></param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ScheduleCalendarInfo(string calendarName, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            CalendarName = calendarName.ValidateArgumentNotNullOrWhitespace(nameof(calendarName));
        }

        /// <inheritdoc cref="ScheduleCalendarInfo"/>
        /// <param name="calendar"><inheritdoc cref="Calendar"/></param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ScheduleCalendarInfo(ICalendar calendar, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            Calendar = calendar.ValidateArgument(nameof(calendar));
        }

        /// <inheritdoc cref="ScheduleInfo"/>
        /// <param name="data">The storage format to convert from</param>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        public ScheduleCalendarInfo(ScheduleCalendarStorageData data, HiveMindOptions options, IMemoryCache cache = null) : this(options, cache)
        {
            _data = data;
        }

        /// <summary>
        /// Ctor for derived classes.
        /// </summary>
        /// <param name="options">The configured options for the environment</param>
        /// <param name="cache">Optional cache that can be used to speed up conversion</param>
        protected ScheduleCalendarInfo(HiveMindOptions options, IMemoryCache cache = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache;
        }
    }
}
