using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Validation;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Templates
{
    /// <summary>
    /// <see cref="ScheduledDaemon"/> that uses settable options of <see cref="TOptions"/> to process.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the daemon</typeparam>
    /// <typeparam name="TOptionsValidator">The type of validator to use for <typeparamref name="TOptions"/></typeparam>
    public abstract class OptionsScheduledDaemon<TOptions, TOptionsValidator> : ScheduledDaemon
        where TOptionsValidator : ValidationProfile<string>
    {
        // Fields
        private readonly object _lock = new object();
        /// <summary>
        /// The validator to use for the options.
        /// </summary>
        protected readonly TOptionsValidator _optionsValidator;

        // Properties
        /// <summary>
        /// The current options used by the daemon.
        /// </summary>
        public TOptions Options { get; private set; }

        /// <inheritdoc cref="OptionsScheduledDaemon{TOptions, TOptionsValidator}"/>
        /// <param name="options">The options to use for the current instance</param>
        /// <param name="optionsValidator">The validator to use to validate new option of type <typeparamref name="TOptions"/></param>
        /// <param name="scheduleBuilder"><inheritdoc cref="ScheduledDaemon.Schedule"/></param>
        /// <param name="scheduleBehaviour"><inheritdoc cref="ScheduledDaemon.Behaviour"/></param>
        /// <param name="allowEmptySchedule"><inheritdoc cref="ScheduledDaemon._allowEmptySchedule"/></param>
        /// <param name="taskManager"><inheritdoc cref="ScheduledDaemon._taskManager"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="ScheduledDaemon._calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="ScheduledDaemon._intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="ScheduledDaemon._hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="ScheduledDaemon._cache"/></param>
        /// <param name="logger"><inheritdoc cref="ScheduledDaemon._logger"/></param>
        protected OptionsScheduledDaemon(TOptions options, TOptionsValidator optionsValidator, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, bool allowEmptySchedule, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null) 
            : base(scheduleBuilder, scheduleBehaviour, allowEmptySchedule, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _optionsValidator = Guard.IsNotNull(optionsValidator);

            SetOptions(options.ValidateArgument(nameof(options)));
        }

        /// <inheritdoc cref="OptionsScheduledDaemon{TOptions, TOptionsValidator}"/>
        /// <param name="options">The options to use for the current instance</param>
        /// <param name="optionsValidator">The validator to use to validate new option of type <typeparamref name="TOptions"/></param>
        /// <param name="schedule"><inheritdoc cref="ScheduledDaemon.Schedule"/></param>
        /// <param name="scheduleBehaviour"><inheritdoc cref="ScheduledDaemon.Behaviour"/></param>
        /// <param name="allowEmptySchedule"><inheritdoc cref="ScheduledDaemon._allowEmptySchedule"/></param>
        /// <param name="taskManager"><inheritdoc cref="ScheduledDaemon._taskManager"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="ScheduledDaemon._calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="ScheduledDaemon._intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="ScheduledDaemon._hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="ScheduledDaemon._cache"/></param>
        /// <param name="logger"><inheritdoc cref="ScheduledDaemon._logger"/></param>
        protected OptionsScheduledDaemon(TOptions options, TOptionsValidator optionsValidator, ISchedule schedule, ScheduleDaemonBehaviour scheduleBehaviour, bool allowEmptySchedule, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
            : base(schedule, scheduleBehaviour, allowEmptySchedule, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _optionsValidator = Guard.IsNotNull(optionsValidator);

            SetOptions(options.ValidateArgument(nameof(options)));
        }

        /// <summary>
        /// Overwrites the options for this instance. Daemon will restart.
        /// </summary>
        /// <param name="options"><inheritdoc cref="Options"/></param>
        public void SetOptions(TOptions options)
        {
            options.ValidateArgument(nameof(options));
            _optionsValidator.Validate(options).Errors.ThrowOnValidationErrors(options);

            lock (_lock)
            {
                Options = options;
                if (State != ScheduledDaemonState.Starting)
                {
                    _logger.Debug("Options changed. Sending stop signal to restart daemon");
                    SignalStop();
                }
            }
        }
    }
}
