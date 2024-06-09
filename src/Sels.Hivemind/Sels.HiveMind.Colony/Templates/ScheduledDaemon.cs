using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Validation;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.HiveMind.Colony.Extensions;
using Sels.Core.Models.Disposables;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Calendar;
using Microsoft.Extensions.Logging;
using Sels.Core.Async.TaskManagement;
using Sels.Core;

namespace Sels.HiveMind.Colony.Templates
{
    /// <summary>
    /// Base class for implementing a daemon that can either run on an interval or be resticted when it's allowed to run.
    /// </summary>
    public abstract class ScheduledDaemon : IDaemonExecutor, IAsyncDisposable
    {
        // Fields
        private readonly object _stopLock = new object();
        private readonly ScheduleValidationProfile _validationProfile;
        /// <summary>
        /// Used to access the configured options for each environment.
        /// </summary>
        protected readonly IOptionsMonitor<HiveMindOptions> _hiveOptions;
        /// <summary>
        /// Used to manage tasks for the daemon.
        /// </summary>
        protected readonly ITaskManager _taskManager;
        /// <summary>
        /// The provider used to resolve intervals.
        /// </summary>
        protected readonly IIntervalProvider _intervalProvider;
        /// <summary>
        /// The provider used to resolve calendars.
        /// </summary>
        protected readonly ICalendarProvider _calendarProvider;
        /// <summary>
        /// Optional cache used to cache resources.
        /// </summary>
        protected readonly IMemoryCache? _cache;
        /// <summary>
        /// Optiona logger for tracing.
        /// </summary>
        protected readonly ILogger? _logger;
        /// <summary>
        /// If the current daemon supports running on an empty schedule. Useful for daemons that run until cancellation.
        /// </summary>
        protected readonly bool _allowEmptySchedule;

        // Stats
        private CancellationTokenSource _stopSource = new CancellationTokenSource();

        // Properties
        /// <summary>
        /// How the current daemon should behave.
        /// </summary>
        public ScheduleDaemonBehaviour Behaviour { get; private set; }
        /// <summary>
        /// The current state of the daemon.
        /// </summary>
        public ScheduledDaemonState State { get; private set; }
        /// <summary>
        /// The current schedule the daemon is running on.
        /// </summary>
        public ISchedule Schedule { get; private set; }

        /// <inheritdoc cref="ScheduledDaemon"/>
        /// <param name="builder">Delegate that is used to configure <see cref="Schedule"/></param>
        /// <param name="behaviour"><inheritdoc cref="Behaviour"/></param>
        /// <param name="taskManager"><inheritdoc cref="_taskManager"/></param>
        /// <param name="allowEmptySchedule"><inheritdoc cref="_allowEmptySchedule"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="_calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="_intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="_hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="_cache"/></param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        protected ScheduledDaemon(Action<IScheduleBuilder> builder, ScheduleDaemonBehaviour behaviour, bool allowEmptySchedule, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null) : this(behaviour, taskManager, intervalProvider, calendarProvider, allowEmptySchedule, validationProfile, hiveOptions, cache, logger)
        {
            SetSchedule(builder);
        }

        /// <inheritdoc cref="ScheduledDaemon"/>
        /// <param name="schedule"><inheritdoc cref="Schedule"/></param>
        /// <param name="behaviour"><inheritdoc cref="Behaviour"/></param>
        /// <param name="taskManager"><inheritdoc cref="_taskManager"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="_hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="_cache"/></param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        protected ScheduledDaemon(ISchedule schedule, ScheduleDaemonBehaviour behaviour, bool allowEmptySchedule, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null) : this(behaviour, taskManager, intervalProvider, calendarProvider, allowEmptySchedule, validationProfile, hiveOptions, cache, logger)
        {
            SetSchedule(schedule);
        }

        private ScheduledDaemon(ScheduleDaemonBehaviour behaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, bool allowEmptySchedule, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache, ILogger? logger)
        {
            _allowEmptySchedule = allowEmptySchedule;
            Behaviour = behaviour;
            _taskManager = taskManager.ValidateArgument(nameof(taskManager));
            _intervalProvider = intervalProvider.ValidateArgument(nameof(intervalProvider));
            _calendarProvider = calendarProvider.ValidateArgument(nameof(calendarProvider));
            _validationProfile = validationProfile.ValidateArgument(nameof(validationProfile));
            _hiveOptions = hiveOptions.ValidateArgument(nameof(hiveOptions));
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Updates the current schedule of the daemon. Will cause the daemon to restart if it's running.
        /// </summary>
        /// <param name="schedule"><inheritdoc cref="Schedule"/></param>
        public void SetSchedule(ISchedule schedule)
        {
            schedule.ValidateArgument(nameof(schedule));

            var result = _validationProfile.Validate(schedule, new ScheduleValidationProfileContext() { AllowEmptySchedule = _allowEmptySchedule });
            if(!result.IsValid) result.Errors.Select(x => $"{x.FullDisplayName}: {x.Message}").ThrowOnValidationErrors(schedule);

            Schedule = schedule;
            _logger.Debug($"Updated schedule");

            lock (_stopLock)
            {
                if (State != ScheduledDaemonState.Starting)
                {
                    _logger.Log($"Stopping deamon to apply new schedule");
                    SignalStop();
                }
            }
        }
        /// <summary>
        /// Updates the current schedule of the daemon. Will cause the daemon to restart if it's running.
        /// </summary>
        /// <param name="builder">Delegate that is used to configure <see cref="Schedule"/></param>
        public void SetSchedule(Action<IScheduleBuilder> builder)
        {
            builder.ValidateArgument(nameof(builder));

            var scheduleBuilder = new ScheduleBuilder(builder, _hiveOptions.CurrentValue, _cache);

            SetSchedule(scheduleBuilder.ScheduleInfo);
        }

        /// <summary>
        /// Signals the deamon to stop. Will cause the daemon to restart if the daemon itself is not requested to stop.
        /// </summary>
        public void SignalStop()
        {
            lock (_stopLock) {
                _logger.Log("Got stop signal");
                _stopSource.Cancel();
                _stopSource = new CancellationTokenSource();
            }
        }

        /// <inheritdoc/>
        public async Task RunUntilCancellation(IDaemonExecutionContext context, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            using var logScope = _logger.TryBeginScope(context);
            bool firstRun = true;
            using var cancellationMonitorScope = token.Register(() =>
            {
                context.Log("Daemon received cancellation request. Sending stop signal");
                SignalStop();
            });
            State = ScheduledDaemonState.Starting;
            context.StateGetter = () => State;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    CancellationToken stopToken = default;
                    if (Schedule.IsEmpty)
                    {
                        context.Log(LogLevel.Debug, "Schedule for deamon is empty. Running normally");
                        State = ScheduledDaemonState.Running;
                        lock (_stopLock)
                        {
                            stopToken = _stopSource.Token;
                        }
                        await Execute(context, stopToken).ConfigureAwait(false);
                        context.Log(LogLevel.Debug, "Daemon stopped gracefully");
                        continue;
                    }

                    DateTime? scheduleDate = null;

                    // Determine schedule date
                    if(firstRun && Behaviour.HasFlag(ScheduleDaemonBehaviour.InstantStart))
                    {
                        var now = DateTime.Now;

                        if(await Schedule.IsInRangeAsync(now, _calendarProvider, _logger, token))
                        {
                            context.Log("Instant start enabled. Starting daemon immediately");
                            scheduleDate = now;
                        }
                        else
                        {
                            context.Log($"Instant start enabled but <{now}> is not in range of the schedule");
                        }
                    }

                    if(!scheduleDate.HasValue)
                    {
                        context.Log("Calculating next schedule date");
                        scheduleDate = await Schedule.GetNextScheduleDateAsync(scheduleDate.Value, context.Daemon.Colony.Options.MaxScheduleTries, true, _intervalProvider, _calendarProvider, _logger, token).ConfigureAwait(false);
                        context.Log($"Daemon will start at <{scheduleDate}>");
                    }
                    firstRun = false;

                    // Sleep until we can run
                    if (scheduleDate > DateTime.Now) 
                    {
                        State = ScheduledDaemonState.Sleeping;
                        context.Log(LogLevel.Debug, $"Daemon sleeping until <{scheduleDate}>");    

                        await Helper.Async.SleepUntil(scheduleDate.Value, token).ConfigureAwait(false);
                    }
                    if(token.IsCancellationRequested)
                    {
                        context.Log(LogLevel.Debug, "Daemon was cancelled while sleeping");
                        return;
                    }

                    // Start
                    State = ScheduledDaemonState.Starting;
                    context.Log("Daemon preparing to start");

                    using var outsideOfRangeMonitor = await StartOutsideOfRangeMonitor(context, scheduleDate.Value, Schedule, token).ConfigureAwait(false);
                    lock (_stopLock)
                    {
                        stopToken = _stopSource.Token;
                    }
                    State = ScheduledDaemonState.Running;
                    await Execute(context, stopToken).ConfigureAwait(false);
                    context.Log(LogLevel.Debug, "Daemon stopped gracefully");
                }
                catch (OperationCanceledException)
                {
                    context.Log(LogLevel.Debug, "Daemon responded to stop signal");
                }
                catch (Exception ex)
                {
                    State = ScheduledDaemonState.Errored;
                    context.Log(LogLevel.Error, "Daemon ran into fatal exception", ex);

                    if (Behaviour.HasFlag(ScheduleDaemonBehaviour.RestartOnException))
                    {
                        context.Log(LogLevel.Warning, $"Restarting after exception", ex);
                    }
                    else
                    {
                        throw;
                    }
                }
                State = ScheduledDaemonState.Stopped;
            }         
        }

        private async Task<IDisposable> StartOutsideOfRangeMonitor(IDaemonExecutionContext context, DateTime scheduleDate, ISchedule schedule, CancellationToken cancellationToken)
        {
            context.ValidateArgument(nameof(context));
            schedule.ValidateArgument(nameof(schedule));
            if(!Behaviour.HasFlag(ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)) return NullDisposer.Instance;

            if(!Schedule.InclusionCalendars.HasValue() && !Schedule.ExclusionCalendars.HasValue())
            {
                context.Log(LogLevel.Debug, $"No calendars configured so daemon doesn't have to be automatically stopped");
                return NullDisposer.Instance;
            }

            context.Log(LogLevel.Debug, $"Calculating when the current daemon should be stopped");
            var nextOutsideOfRange = await schedule.GetNextDateOutsideOfRangeAsync(scheduleDate, context.Daemon.Colony.Options.MaxScheduleTries, _calendarProvider, _logger, cancellationToken);
            context.Log($"Daemon will be automatically stopped after <{nextOutsideOfRange}>");
            return _taskManager.ScheduleDelayed(nextOutsideOfRange, (t, c) =>
            {
                context.Log($"Daemon is outside of it's allowed range. Signaling stop");
                SignalStop();
                return (IManagedTask)null;
            });
        }

        /// <summary>
        /// Executes the daemon.
        /// </summary>
        /// <param name="context">The context of the deamon currently executing</param>
        /// <param name="token">Token that will be cancelled when the daemon is requested to stop</param>
        public abstract Task Execute(IDaemonExecutionContext context, CancellationToken token);

        /// <inheritdoc/>
        public async ValueTask DisposeAsync() => await _taskManager.StopAllForAsync(this).ConfigureAwait(false);
    }

    /// <summary>
    /// The current state of a <see cref="ScheduledDaemon"/>.
    /// </summary>
    public enum ScheduledDaemonState
    {
        /// <summary>
        /// Deamon is starting.
        /// </summary>
        Starting = 0,
        /// <summary>
        /// Daemon is currently running.
        /// </summary>
        Running = 1,
        /// <summary>
        /// Daemon is currently sleeping according to the schedule.
        /// </summary>
        Sleeping = 2,
        /// <summary>
        /// Daemon failed with a fatal exception.
        /// </summary>
        Errored = 3,
        /// <summary>
        /// Daemon is currently stopped.
        /// </summary>
        Stopped = 4
    }

    /// <summary>
    /// Contains options on how a <see cref="ScheduledDaemon"/> should behave.
    /// </summary>
    [Flags]
    public enum ScheduleDaemonBehaviour
    {
        /// <summary>
        /// No behaviours selected.
        /// </summary>
        None = 0,
        /// <summary>
        /// If the daemon should start executing immediately the first time it's launched. When set to false the next execution date will be determined by the schedule.
        /// </summary>
        InstantStart = 1,
        /// <summary>
        /// If the daemon should stop if it's running outside of the schedule.
        /// </summary>
        StopIfOutsideOfSchedule = 2,
        /// <summary>
        /// If the daemon should restart if it encounters an exception. Otherwise it's rethrown and the restart will be handled by the colony. (based on the policy)
        /// </summary>
        RestartOnException = 4
    }
}
