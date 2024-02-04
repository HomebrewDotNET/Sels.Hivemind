using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Threading;
using Sels.HiveMind.Calendar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Calendar that uses weekdays to determine if a date is within the range.
    /// </summary>
    public class WeekdayCalendar : ICalendar
    {
        // Statics
        private static readonly DayOfWeek[] AllDays = Helper.Enums.GetAll<DayOfWeek>().OrderBy(x => x == DayOfWeek.Sunday).ThenBy(x => x).ToArray();
        private static readonly int MaxDaysInWeek = 7;

        // Properties
        /// <summary>
        /// The weekdays that are in range.
        /// </summary>
        public IReadOnlyCollection<DayOfWeek> Weekdays { get; }

        /// <inheritdoc cref="WeekdayCalendar"/>
        /// <param name="days"><inheritdoc cref="Weekdays"/></param>
        public WeekdayCalendar(IEnumerable<DayOfWeek> days)
        {
            Weekdays = days.ValidateArgumentNotNullOrEmpty(nameof(days)).OrderBy(x => x).Distinct().ToArray();
            if (Weekdays.Count == MaxDaysInWeek)
            {
                throw new ArgumentException("Weekday calendar can't contain all days of the week", nameof(days));
            }
        }

        /// <inheritdoc cref="WeekdayCalendar"/>
        /// <param name="days"><inheritdoc cref="Weekdays"/></param>
        public WeekdayCalendar(params DayOfWeek[] days) : this(days.AsEnumerable())
        {
        }

        /// <inheritdoc/>
        public Task<bool> IsInRange(DateTime date)
        {
            return Task.FromResult(Weekdays.Contains(date.DayOfWeek));
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetNextInRange(DateTime date)
        {
            if (await IsInRange(date).ConfigureAwait(false)) return date;

            // Count how many days until the next weekday in range
            var daysToAdd = 0;
            var daysAfter = GetDaysAfter(date.DayOfWeek).ToArray();
            foreach (var day in daysAfter)
            {
                daysToAdd++;
                if (Weekdays.Contains(day))
                {
                    break;
                }
            }

            // Move date to selected day
            return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, date.Kind).AddDays(daysToAdd);
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetNextOutsideOfRange(DateTime date)
        {
            if (!await IsInRange(date).ConfigureAwait(false)) return date;

            // Count how many days until the next weekday outside of range
            var daysToAdd = 0;
            var daysAfter = GetDaysAfter(date.DayOfWeek).ToArray();
            foreach (var day in daysAfter)
            {
                daysToAdd++;
                if (!Weekdays.Contains(day))
                {
                    break;
                }
            }

            // Move date to selected day
            return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, date.Kind).AddDays(daysToAdd);
        }

        private IEnumerable<DayOfWeek> GetDaysAfter(DayOfWeek day)
        {
            var index = Array.IndexOf(AllDays, day);

            return AllDays.Where(x => x != day).OrderBy(x => Array.IndexOf(AllDays, x) < index);
        }


    }
}
