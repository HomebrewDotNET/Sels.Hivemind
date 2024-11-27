using Sels.Core.Extensions;
using Sels.Core.Extensions.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Calendar that defines a range by defining days.
    /// </summary>
    public class DayCalendar : ICalendar
    {
        // Properties
        /// <summary>
        /// The days that are included in the range.
        /// </summary>
        public IReadOnlyCollection<(int DayOfMonth, int? Month, int? Year)> Days { get; }

        /// <inheritdoc cref="DayCalendar"/>
        /// <param name="days"><inheritdoc cref="Days"/></param>
        /// <exception cref="ArgumentException"></exception>
        public DayCalendar(IEnumerable<(int DayOfMonth, int? Month, int? Year)> days)
        {
            Days = days.ValidateArgumentNotNullOrEmpty(nameof(days)).Select((x, i) =>
            {
                if(x.DayOfMonth < 1 || x.DayOfMonth > 31)
                {
                    throw new ArgumentException($"Day of month {x.DayOfMonth} is not valid", $"{nameof(days)}[{i}]");
                }
                if(x.Month.HasValue)
                {
                    if(x.Month < 1 || x.Month > 12)
                    {
                        throw new ArgumentException($"Month {x.Month} is not valid", $"{nameof(days)}[{i}]");
                    }

                    // Check if day is valid for month
                    if(x.DayOfMonth > DateTime.DaysInMonth(x.Year ?? DateTime.Now.Year, x.Month.Value))
                    {
                        throw new ArgumentException($"Day of month {x.DayOfMonth} is not valid for month {x.Month}", $"{nameof(days)}[{i}]");
                    }
                }
                if(x.Year.HasValue)
                {
                    if(x.Year < 1)
                    {
                        throw new ArgumentException($"Year {x.Year} is not valid", $"{nameof(days)}[{i}]");
                    }
                }
                return x;
            }).ToArray();
        }

        /// <inheritdoc cref="DayCalendar"/>
        /// <param name="days"><inheritdoc cref="Days"/></param>
        /// <exception cref="ArgumentException"></exception>
        public DayCalendar(params (int DayOfMonth, int? Month, int? Year)[] days) : this(days.AsEnumerable())
        {

        }
        /// <inheritdoc/>
        public Task<bool> IsInRangeAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            return Days.Any(x =>
            {
                if(x.DayOfMonth != date.Day)
                {
                    return false;
                }

                if (x.Month.HasValue && date.Month != x.Month.Value)
                {
                    return false;
                }

                if (x.Year.HasValue && date.Year != x.Year.Value)
                {
                    return false;
                }

                return true;
            }).ToTaskResult();
        }
        /// <inheritdoc/>
        public async Task<DateTime> GetNextInRangeAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            if(await IsInRangeAsync(date).ConfigureAwait(false)) return date;

            var possibleDays = GetAfter(date).Select(x =>
            {
                DateTime newDate;
                if (x.Year.HasValue)
                {
                    if (x.Month.HasValue)
                    {
                        return new DateTime(x.Year.Value, x.Month.Value, x.DayOfMonth);
                    }
                    else
                    {
                        newDate = new DateTime(x.Year.Value, date.Month, x.DayOfMonth);
                        if (newDate < date)
                        {
                            return SetNextPossibleMonth(newDate);
                        }
                        return newDate;
                    }
                }

                if (x.Month.HasValue)
                {
                    newDate = new DateTime(date.Year, x.Month.Value, x.DayOfMonth);
                    if (newDate < date)
                    {
                        return SetNextPossibleYear(newDate);
                    }
                    return newDate;
                }

                newDate = new DateTime(date.Year, date.Month, x.DayOfMonth);
                if (newDate < date)
                {
                    return SetNextPossibleMonth(newDate);
                }
                return newDate;
            }).Where(x => x > date);

            return possibleDays.Min();
        }

        private DateTime SetNextPossibleMonth(DateTime date)
        {
            var day = date.Day;
            var newMonth = date.Month + 1;
            var newYear = date.Year;
            if (newMonth > 12)
            {
                newMonth = 1;
                newYear++;
            }

            while (DateTime.DaysInMonth(newYear, newMonth) < day)
            {
                newMonth++;
                if (newMonth > 12)
                {
                    newMonth = 1;
                    newYear++;
                }
            }

            return new DateTime(newYear, newMonth, day);
        }

        private DateTime SetNextPossibleYear(DateTime date)
        {
            var day = date.Day;
            var newMonth = date.Month;
            var newYear = date.Year +1;

            while (DateTime.DaysInMonth(newYear, newMonth) < day)
            {
                newYear++;
            }

            return new DateTime(newYear, newMonth, day);
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetNextOutsideOfRangeAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            if (!(await IsInRangeAsync(date).ConfigureAwait(false))) return date;

            var nextDate = new DateTime(date.Year, date.Month, date.Day).AddDays(1);
            while(await IsInRangeAsync(nextDate).ConfigureAwait(false))
            {
                nextDate = nextDate.AddDays(1);
            }
            return nextDate;
        }

        private IEnumerable<(int DayOfMonth, int? Month, int? Year)> GetAfter(DateTime date)
        {
            var daysAfter = Days.Where(x =>
            {
                if (x.Year.HasValue)
                {
                    if(x.Year < date.Year)
                    {
                        return false;
                    }
                    else if (x.Month.HasValue && x.Year == date.Year && date.Month < x.Month.Value)
                    {
                        return false;
                    }
                }

                return true;
            });

            var dayOfMonth = date.Day;
            var month = date.Month;
            var year = date.Year;

            return daysAfter;
        }
    }
}
