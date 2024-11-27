using Sels.HiveMind.Calendar;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Defines a daily timeframe by having a start time and an end time.
    /// </summary>
    public class DailyTimeframeCalendar : ICalendar
    {
        // Properties
        /// <summary>
        /// Timespan that defines the start of the timeframe. Only hours, minutes and seconds should be set and should be smaller than 24 hours.
        /// </summary>
        public TimeSpan StartTime { get;}
        /// <summary>
        /// Timespan that defines the end of the timeframe. Only hours, minutes and seconds should be set and should be smaller than 24 hours
        /// </summary>
        public TimeSpan EndTime { get; }

        /// <inheritdoc cref="DailyTimeframeCalendar"/>
        /// <param name="startTime"><inheritdoc cref="StartTime"/></param>
        /// <param name="endTime"><inheritdoc cref="EndTime"/></param>
        /// <exception cref="ArgumentException"></exception>
        public DailyTimeframeCalendar(TimeSpan startTime, TimeSpan endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
            if(StartTime < TimeSpan.Zero)
            {
                throw new ArgumentException("Start time cannot be negative", nameof(startTime));
            }
            if(EndTime < TimeSpan.Zero)
            {
                throw new ArgumentException("End time cannot be negative", nameof(endTime));
            }
            if(StartTime >= TimeSpan.FromHours(24))
            {
                throw new ArgumentException("Start time cannot be larger or equal to 24 hours", nameof(startTime));
            }
            if(EndTime >= TimeSpan.FromHours(24))
            {
                throw new ArgumentException("End time cannot be larger or equal to 24 hours", nameof(endTime));
            }
            if(StartTime.Milliseconds != 0)
            {
                throw new ArgumentException("Start time cannot have milliseconds", nameof(startTime));
            }
            if(EndTime.Milliseconds != 0)
            {
                throw new ArgumentException("End time cannot have milliseconds", nameof(endTime));
            }
            if(StartTime == EndTime)
            {
                throw new ArgumentException("Start time and end time cannot be the same", nameof(endTime));
            }
        }

        /// <inheritdoc/>
        public Task<bool> IsInRangeAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var time = new TimeSpan(date.Hour, date.Minute, date.Second);

            if(StartTime < EndTime)
            {
                return Task.FromResult(StartTime <= time && time < EndTime);
            }
            else
            {
                return Task.FromResult(time >= StartTime || time < EndTime);
            }
        }
        /// <inheritdoc/>
        public async Task<DateTime> GetNextInRangeAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            if (await IsInRangeAsync(date).ConfigureAwait(false)) return date;

            var time = new TimeSpan(date.Hour, date.Minute, date.Second);

            if (StartTime < EndTime && time > EndTime)
            {
                return new DateTime(date.Year, date.Month, date.Day, StartTime.Hours, StartTime.Minutes, StartTime.Seconds, date.Kind).AddDays(1);
            }
            else 
            {
                return new DateTime(date.Year, date.Month, date.Day, StartTime.Hours, StartTime.Minutes, StartTime.Seconds, date.Kind);
            }
        }
        /// <inheritdoc/>
        public async Task<DateTime> GetNextOutsideOfRangeAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            if (!(await IsInRangeAsync(date).ConfigureAwait(false))) return date;

            var time = new TimeSpan(date.Hour, date.Minute, date.Second);

            if (StartTime > EndTime && time >= StartTime)
            {
                return new DateTime(date.Year, date.Month, date.Day, EndTime.Hours, EndTime.Minutes, EndTime.Seconds, date.Kind).AddDays(1);
            }
            else
            {
                return new DateTime(date.Year, date.Month, date.Day, EndTime.Hours, EndTime.Minutes, EndTime.Seconds, date.Kind);
            }      
        }
    }
}
