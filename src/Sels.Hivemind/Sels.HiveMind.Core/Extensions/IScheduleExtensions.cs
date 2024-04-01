using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Interval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.HiveMind.HiveMindConstants;

namespace Sels.HiveMind.Schedule
{
    /// <summary>
    /// Contains static extension method for <see cref="ISchedule"/>.
    /// </summary>
    public static class IScheduleExtensions
    {
        /// <summary>
        /// Tries to generate the next schedule date based on the <paramref name="schedule"/> and <paramref name="lastDate"/>.
        /// </summary>
        /// <param name="schedule">The schedule configuration to use</param>
        /// <param name="lastDate">The last schedule date. Used to determine the next date</param>
        /// <param name="maxTryAmount">How many times to try and generate a schedule date. Used to avoid infinite loops when dealing with invalid schedules</param>
        /// <param name="alwaysUseInterval">If the interval should always be used to generate the next. Only used when calendars are also defined. When set to false the dates determined by the calendars are also considered valid schedule dates, when set to true the interval is applied on dates determined by the calendars</param>
        /// <param name="intervalProvider">Provider to use to resolve intervals</param>
        /// <param name="calendarProvider">Provider to use to resolve calendars</param>
        /// <param name="logger">Optional logger for tracing</param>
        /// <param name="cancellationToken">Optional token to cancel the request</param>
        /// <returns>The next schedule date determined by <paramref name="schedule"/> and <paramref name="lastDate"/></returns>
        public static async Task<DateTime> GetNextScheduleDateAsync(this ISchedule schedule, DateTime lastDate, int maxTryAmount, bool alwaysUseInterval, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ILogger logger = default, CancellationToken cancellationToken = default)
        {
            schedule.ValidateArgument(nameof(schedule));
            maxTryAmount.ValidateArgumentLargerOrEqual(nameof(maxTryAmount), 1);
            intervalProvider.ValidateArgument(nameof(intervalProvider));
            calendarProvider.ValidateArgument(nameof(calendarProvider));

            DateTime? nextDate = null;
            var disposables = new List<IAsyncDisposable>();
            IInterval interval = null;
            List<ICalendar> inclusionCalendars = null;
            List<ICalendar> exclusionCalendars = null;
            try
            {
                // Resolve components
                if(schedule.IntervalName.HasValue())
                {
                    var intervalScope = await intervalProvider.GetIntervalAsync(schedule.IntervalName, cancellationToken).ConfigureAwait(false);
                    disposables.Add(intervalScope);
                    interval = intervalScope.Component;
                }

                if(schedule.InclusionCalendars.HasValue())
                {
                    inclusionCalendars ??= new List<ICalendar>();
                    foreach (var calendar in schedule.InclusionCalendars)
                    {                        
                        if (calendar.CalendarName.HasValue())
                        {
                            var calendarScope = await calendarProvider.GetCalendarAsync(calendar.CalendarName, cancellationToken).ConfigureAwait(false);
                            disposables.Add(calendarScope);
                            inclusionCalendars.Add(calendarScope.Component);
                        }
                        else
                        {
                            inclusionCalendars.Add(calendar.Calendar);
                            if(calendar.Calendar is IAsyncDisposable asyncDisposable)
                            {
                                disposables.Add(asyncDisposable);
                            }
                        }
                    }
                }

                if (schedule.ExclusionCalendars.HasValue())
                {
                    exclusionCalendars ??= new List<ICalendar>();
                    foreach (var calendar in schedule.ExclusionCalendars)
                    {
                        if (calendar.CalendarName.HasValue())
                        {
                            var calendarScope = await calendarProvider.GetCalendarAsync(calendar.CalendarName, cancellationToken).ConfigureAwait(false);
                            disposables.Add(calendarScope);
                            exclusionCalendars.Add(calendarScope.Component);
                        }
                        else
                        {
                            exclusionCalendars.Add(calendar.Calendar);
                            if (calendar.Calendar is IAsyncDisposable asyncDisposable)
                            {
                                disposables.Add(asyncDisposable);
                            }
                        }
                    }
                }

                // Generate next date
                logger.Log($"Trying to determine the next schedule date after <{lastDate}> using {(interval != null ? $"interval <{schedule.IntervalName}> ({schedule.Interval})" : "no internal")}, <{inclusionCalendars?.Count ?? 0}> inclusion calendars and <{exclusionCalendars?.Count ?? 0}>");

                if(interval != null)
                {
                    nextDate = await GenerateNextScheduleDateUsingInterval(lastDate, interval, schedule.Interval, alwaysUseInterval, inclusionCalendars?.ToArray() ?? Array.Empty<ICalendar>(), exclusionCalendars?.ToArray() ?? Array.Empty<ICalendar>(), maxTryAmount, logger, cancellationToken).ConfigureAwait(false);
                }
                else if (inclusionCalendars.HasValue())
                {
                    nextDate = await GenerateNextScheduleDateUsingInclusionCalendars(lastDate, inclusionCalendars.ToArray(), exclusionCalendars?.ToArray() ?? Array.Empty<ICalendar>(), maxTryAmount, logger, cancellationToken).ConfigureAwait(false);
                }
                else if (exclusionCalendars.HasValue())
                {
                    nextDate = await GenerateNextScheduleDateUsingExclusionCalendars(lastDate, exclusionCalendars.ToArray(), maxTryAmount, logger, cancellationToken).ConfigureAwait(false);
                }

                if (nextDate.HasValue)
                {
                    logger.Log($"Next schedule date after <{lastDate}> using {(interval != null ? $"interval <{schedule.IntervalName}> ({schedule.Interval})" : "no internal")}, <{inclusionCalendars?.Count ?? 0}> inclusion calendars and <{exclusionCalendars?.Count ?? 0}> is <{nextDate}>");
                    return nextDate.Value;
                }
                else
                {
                    throw new InvalidOperationException($"Could not determine the next schedule date after <{lastDate}> using {(interval != null ? $"interval <{schedule.IntervalName}> ({schedule.Interval})" : "no internal")}, <{inclusionCalendars?.Count ?? 0}> inclusion calendars and <{exclusionCalendars?.Count ?? 0}> using <{maxTryAmount}> tries");
                }
            }
            finally
            {
                await disposables.ForceExecuteAsync(x => x.DisposeAsync().AsTask(), (d, e) => logger.Log($"Could not properly dispose <{d}>", e)).ConfigureAwait(false);
            }
        }

        private static async Task<DateTime?> GenerateNextScheduleDateUsingInterval(DateTime currentDate, IInterval interval, object intervalInput, bool alwaysUseInterval, ICalendar[] inclusionCalendars, ICalendar[] exclusionCalendars, int maxTryAmount, ILogger logger = default, CancellationToken cancellationToken = default)
        {
            interval.ValidateArgument(nameof(interval));
            interval.ValidateArgument(nameof(intervalInput));
            inclusionCalendars.ValidateArgument(nameof(inclusionCalendars));
            exclusionCalendars.ValidateArgument(nameof(exclusionCalendars));
            maxTryAmount.ValidateArgumentLargerOrEqual(nameof(maxTryAmount), 1);

            logger.Debug($"Generating next schedule date using interval <{interval}> ({intervalInput}) after <{currentDate}>");

            var currentTry = 0;
            DateTime? nextDate = null;
            bool lastDateIsCalendar = false;
            
            while (!nextDate.HasValue && currentTry < maxTryAmount)
            {
                currentTry++;
                var lastDate = currentDate;

                if(!lastDateIsCalendar || alwaysUseInterval)
                {
                    currentDate = await interval.GetNextDateAsync(lastDate, intervalInput, cancellationToken).ConfigureAwait(false);
                    logger.Debug($"Interval <{interval}> ({intervalInput}) generated schedule date <{currentDate}> after last schedule date <{lastDate}>. Checking against calendars if defined");
                }
                else
                {
                    logger.Debug($"Schedule date <{currentDate}> was generated by calendars. Checking against calendars");
                }
                
                bool allowedToRun = false;
                List<DateTime> nextPossibleDates = new List<DateTime>();

                // Check if allowed to run
                if(inclusionCalendars.HasValue())
                {
                    foreach(var calendar in inclusionCalendars)
                    {
                        if(await calendar.IsInRangeAsync(currentDate, cancellationToken).ConfigureAwait(false))
                        {
                            logger.Debug($"Schedule date <{currentDate}> is in range of inclusion calendar <{calendar}>");
                            allowedToRun = true;
                        }
                        else
                        {
                            logger.Debug($"Schedule date <{currentDate}> is not in range of inclusion calendar <{calendar}>. Generating next possible date");
                            var nextPossibleDate = await calendar.GetNextInRangeAsync(currentDate, cancellationToken).ConfigureAwait(false);
                            logger.Debug($"Next possible date after <{currentDate}> for inclusion calendar <{calendar}> is <{nextPossibleDate}>");
                            nextPossibleDates.Add(nextPossibleDate);
                        }
                    }
                }
                else
                {
                    allowedToRun = true;
                }

                // Not allowed to run according to inclusion calendars so pick the next earliest possible date
                if (!allowedToRun)
                {
                    // We have exclusion calendars so we need to check if the next possible date is not in range of any exclusion calendars
                    if (exclusionCalendars.HasValue())
                    {
                        List<DateTime> nextPossibleExclusionDates = new List<DateTime>();

                        foreach(var nextPossibleDate in nextPossibleDates)
                        {
                            foreach (var calendar in exclusionCalendars)
                            {
                                if (await calendar.IsInRangeAsync(nextPossibleDate, cancellationToken).ConfigureAwait(false))
                                {
                                    logger.Debug($"Next possible date <{nextPossibleDate}> is in range of exclusion calendar <{calendar}>. Generating next possible date outside of range");
                                    var nextPossibleExclusionDate = await calendar.GetNextOutsideOfRangeAsync(currentDate, cancellationToken).ConfigureAwait(false);
                                    logger.Debug($"Next possible date after <{nextPossibleDate}> for exclusion calendar <{calendar}> is <{nextPossibleDate}>");
                                    nextPossibleExclusionDates.Add(nextPossibleExclusionDate);
                                    nextPossibleDates.Remove(nextPossibleDate);
                                }
                                else
                                {
                                    logger.Debug($"Next possible date <{currentDate}> is not excluded by calendar <{calendar}>");
                                }
                            }
                        }
                        
                        if(nextPossibleDates.HasValue())
                        {
                            currentDate = nextPossibleDates.Min();
                            lastDateIsCalendar = true;
                            continue;
                        }
                        else
                        {
                            currentDate = nextPossibleExclusionDates.Min();
                            lastDateIsCalendar = true;
                            continue;
                        }
                    }
                    // No exclusion calendars so we can just pick the next possible date
                    else
                    {
                        currentDate = nextPossibleDates.Min();
                        lastDateIsCalendar = true;
                        continue;
                    }
                }
                // Allowed to run so check if not in range of exclusion calendars
                else if(exclusionCalendars.HasValue())
                {
                    foreach(var calendar in exclusionCalendars)
                    {
                        if(await calendar.IsInRangeAsync(currentDate, cancellationToken).ConfigureAwait(false))
                        {
                            logger.Debug($"Schedule date <{currentDate}> is in range of exclusion calendar <{calendar}>. Generating next possible date");
                            var nextPossibleDate = await calendar.GetNextOutsideOfRangeAsync(currentDate, cancellationToken).ConfigureAwait(false);
                            logger.Debug($"Next possible date after <{currentDate}> for exclusion calendar <{calendar}> is <{nextPossibleDate}>");
                            nextPossibleDates.Add(nextPossibleDate);
                        }
                        else
                        {
                            logger.Debug($"Schedule date <{currentDate}> is not excluded by calendar <{calendar}>");
                        }
                    }

                    // Excluded by all calendars so pick the next possible date
                    if(nextPossibleDates.HasValue())
                    {
                        currentDate = nextPossibleDates.Min();
                        lastDateIsCalendar = true;
                        continue;
                    }
                }

                nextDate = currentDate;
            }

            return nextDate;
        }

        private static async Task<DateTime?> GenerateNextScheduleDateUsingInclusionCalendars(DateTime currentDate, ICalendar[] inclusionCalendars, ICalendar[] exclusionCalendars, int maxTryAmount, ILogger logger = default, CancellationToken cancellationToken = default)
        {
            inclusionCalendars.ValidateArgumentNotNullOrEmpty(nameof(inclusionCalendars));
            exclusionCalendars.ValidateArgument(nameof(exclusionCalendars));
            maxTryAmount.ValidateArgumentLargerOrEqual(nameof(maxTryAmount), 1);

            logger.Debug($"Generating next schedule date using <{inclusionCalendars.Length}> inclusion calendars after <{currentDate}>");

            var currentTry = 0;
            DateTime? nextDate = null;

            while(!nextDate.HasValue && currentTry < maxTryAmount)
            {
                currentTry++;
                var lastDate = currentDate;

                // Generate next dates in range of inclusion calendars
                var dates = new List<DateTime>();
                foreach(var calendar in inclusionCalendars)
                {
                    var dateToSchedule = currentDate;
                    if(await calendar.IsInRangeAsync(dateToSchedule, cancellationToken).ConfigureAwait(false))
                    {
                        logger.Debug($"Schedule date <{currentDate}> already in range of calendar <{calendar}>. Using next outside range to determine schedule date");
                        dateToSchedule = await calendar.GetNextOutsideOfRangeAsync(currentDate, cancellationToken).ConfigureAwait(false);
                    }
                    var nextDateInRange = await calendar.GetNextInRangeAsync(dateToSchedule, cancellationToken).ConfigureAwait(false);
                    logger.Debug($"Next possible schedule date after <{lastDate}> for calendar is <{nextDateInRange}>");
                    dates.Add(nextDateInRange);
                }

                // Check if any of the next dates are in range of exclusion calendars
                if (exclusionCalendars.HasValue())
                {
                    var nextPossibleDates = new List<DateTime>();
                    foreach(var date in dates)
                    {
                        foreach(var calendar in exclusionCalendars)
                        {
                            if(await calendar.IsInRangeAsync(date, cancellationToken).ConfigureAwait(false))
                            {
                                logger.Debug($"Next possible schedule date <{date}> is excluded by calendar <{calendar}>. Generating next possible date");
                                var nextPossibleDate = await calendar.GetNextOutsideOfRangeAsync(date, cancellationToken).ConfigureAwait(false);
                                logger.Debug($"Next possible schedule date after <{date}> for calendar <{calendar}> is <{nextPossibleDate}>");
                                nextPossibleDates.Add(nextPossibleDate);
                                dates.Remove(date);
                            }
                            else
                            {
                                logger.Debug($"Schedule date <{date}> is not excluded by calendar <{calendar}>");
                            }
                        }

                        // At least 1 not excluded date
                        if (dates.HasValue())
                        {
                            nextDate = dates.Min();
                        }
                        else
                        {
                            currentDate = nextPossibleDates.Min();
                        }
                    }
                }
                else
                {
                    // Take earliest date
                    nextDate = dates.Min();
                }
            }

            return nextDate;
        }

        private static async Task<DateTime?> GenerateNextScheduleDateUsingExclusionCalendars(DateTime currentDate, ICalendar[] exclusionCalendars, int maxTryAmount, ILogger logger = default, CancellationToken cancellationToken = default)
        {
            exclusionCalendars.ValidateArgumentNotNullOrEmpty(nameof(exclusionCalendars));
            maxTryAmount.ValidateArgumentLargerOrEqual(nameof(maxTryAmount), 1);

            logger.Debug($"Generating next schedule date using <{exclusionCalendars.Length}> exclusion calendars after <{currentDate}>");

            var currentTry = 0;
            DateTime? nextDate = null;

            while (!nextDate.HasValue && currentTry < maxTryAmount)
            {
                currentTry++;
                var lastDate = currentDate;

                // Generate next dates outside of exclusion calendars
                var dates = new List<DateTime>();
                foreach (var calendar in exclusionCalendars)
                {
                    var dateToSchedule = currentDate;
                    if(!await calendar.IsInRangeAsync(dateToSchedule, cancellationToken).ConfigureAwait(false))
                    {
                        logger.Debug($"Schedule date <{currentDate}> already outside of range of calendar <{calendar}>. Using next in range to determine schedule date");
                        dateToSchedule = await calendar.GetNextInRangeAsync(dateToSchedule, cancellationToken).ConfigureAwait(false);
                    }
                    var nextDateOutsideOfRange = await calendar.GetNextOutsideOfRangeAsync(dateToSchedule, cancellationToken).ConfigureAwait(false);
                    logger.Debug($"Next possible schedule date after <{lastDate}> for calendar is <{nextDateOutsideOfRange}>");
                    dates.Add(nextDateOutsideOfRange);
                }

                // Double check if any of the next dates are in range of any of the calendars
                foreach(var date in dates)
                {
                    var nextPossibleDates = new List<DateTime>();
                    foreach (var calendar in exclusionCalendars)
                    {
                        if (await calendar.IsInRangeAsync(date, cancellationToken).ConfigureAwait(false))
                        {
                            logger.Debug($"Next possible schedule date <{date}> is excluded by calendar <{calendar}>. Generating next possible date");
                            var nextPossibleDate = await calendar.GetNextOutsideOfRangeAsync(date, cancellationToken).ConfigureAwait(false);
                            logger.Debug($"Next possible schedule date after <{date}> for calendar <{calendar}> is <{nextPossibleDate}>");
                            nextPossibleDates.Add(nextPossibleDate);
                            dates.Remove(date);
                        }
                        else
                        {
                            logger.Debug($"Schedule date <{date}> is not excluded by calendar <{calendar}>");
                        }
                    }

                    // Take earliest date
                    if (dates.HasValue())
                    {
                        nextDate = dates.Min();
                    }
                    else
                    {
                        currentDate = nextPossibleDates.Min();
                    }
                }
            }

            return nextDate;
        }
    }
}
