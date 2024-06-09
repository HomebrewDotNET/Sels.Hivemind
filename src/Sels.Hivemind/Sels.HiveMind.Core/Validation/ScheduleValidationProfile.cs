using Sels.Core.Extensions;
using Sels.HiveMind.Models.Storage.Schedule;
using Sels.HiveMind.Schedule;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Validation
{
    /// <summary>
    /// Contains the validation rules for schedules.
    /// </summary>
    public class ScheduleValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="ScheduleValidationProfile"/>
        public ScheduleValidationProfile()
        {
            CreateValidationFor<ISchedule>()
                .ForProperty(x => x.IntervalName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Interval)
                    .NextWhen(x => x.Source.IntervalName != null)
                    .CannotBeNull(x => $"Cannot be null when <{nameof(x.Source.IntervalName)}> is set")
                .ForSource()
                    .WithContext<ScheduleValidationProfileContext>()
                    .NextWhen(x => !x.HasContext || !x.Context.AllowEmptySchedule)
                    .ValidIf(x =>
                    {
                        var hasName = x.Source.IntervalName != null;
                        var hasInclusionCalendar = x.Source.InclusionCalendars.HasValue();
                        var hasExclusionCalendar = x.Source.ExclusionCalendars.HasValue();

                        return hasName || hasInclusionCalendar || hasExclusionCalendar;
                    }, x => $"Either <{nameof(x.Source.IntervalName)}>, <{nameof(x.Source.InclusionCalendars)}> or <{nameof(x.Source.ExclusionCalendars)}> need to be set");

            CreateValidationFor<IScheduleCalendar>()
                .ForProperty(x => x.CalendarName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace()
                .ForSource()
                    .ValidIf(x =>
                    {
                        var hasName = x.Source.CalendarName != null;
                        var hasCalendar = x.Source.Calendar != null;

                        return hasName != hasCalendar;
                    }, x => $"Either <{nameof(x.Source.CalendarName)}> or <{nameof(x.Source.Calendar)}> has to be set but noth both");

            CreateValidationFor<ScheduleStorageData>()
                .ForProperty(x => x.IntervalName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.IntervalTypeName)
                    .NextWhen(x => x.Source.IntervalName.HasValue())
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.IntervalStorageValue)
                    .NextWhen(x => x.Source.IntervalTypeName.HasValue())
                    .CannotBeNullOrWhitespace()
                .ForSource()
                    .WithContext<ScheduleValidationProfileContext>()
                    .NextWhen(x => !x.HasContext || !x.Context.AllowEmptySchedule)
                    .ValidIf(x =>
                    {
                        var hasName = x.Source.IntervalName != null;
                        var hasInclusionCalendar = x.Source.InclusionCalendars.HasValue();
                        var hasExclusionCalendar = x.Source.ExclusionCalendars.HasValue();

                        return hasName || hasInclusionCalendar || hasExclusionCalendar;
                    }, x => $"Either <{nameof(x.Source.IntervalName)}>, <{nameof(x.Source.InclusionCalendars)}> or <{nameof(x.Source.ExclusionCalendars)}> need to be set");

            CreateValidationFor<ScheduleCalendarStorageData>()
                .ForProperty(x => x.CalendarName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.CalendarTypeName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.CalendarStorageValue)
                    .NextWhen(x => x.Source.CalendarTypeName.HasValue())
                    .CannotBeNullOrWhitespace()
                 .ForSource()
                    .ValidIf(x =>
                    {
                        var hasName = x.Source.CalendarName != null;
                        var hasCalendar = x.Source.CalendarTypeName != null;

                        return hasName != hasCalendar;
                    }, x => $"Either <{nameof(x.Source.CalendarName)}> or <{nameof(x.Source.CalendarTypeName)}> has to be set but noth both");
        }
    }

    /// <summary>
    /// Optional context for <see cref="ScheduleValidationProfile"/> to modify validation rules based on the context.
    /// </summary>
    public class ScheduleValidationProfileContext
    {
        /// <summary>
        /// If schedules are allowed to be empty. (No interval and calendars)
        /// </summary>
        public bool AllowEmptySchedule { get; set; }
    }
}
