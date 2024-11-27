using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.Recurring
{
    /// <summary>
    /// A swarm that executes pending recurring jobs where the options are managed by <see cref="AutoCreateRecurringJobWorkerSwarmHostOptions"/>.
    /// </summary>
    public class AutoManagedRecurringJobWorkerSwarmHost : RecurringJobWorkerSwarmHost
    {
        // Fields
        private readonly AutoCreateRecurringJobWorkerSwarmHostOptions _options;

        // State 
        private bool _autoManaged = true;

        /// <inheritdoc cref="AutoManagedRecurringJobWorkerSwarmHost"/>
        /// <param name="autoManagedOptions">The options that will be used to auto manage the current swarm</param>
        /// <param name="client"><inheritdoc cref="_client"></inheritdoc></param>
        /// <param name="queueType"><inheritdoc cref="SwarmHost{TOptions, TDefaultOptions}.QueueType"/></param>
        /// <param name="defaultOptions"><inheritdoc cref="SwarmHost{TOptions, TDefaultOptions}._defaultOptions"/></param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        /// <param name="scheduleBuilder"><inheritdoc cref="ScheduledDaemon.Schedule"/></param>
        /// <param name="scheduleBehaviour"><inheritdoc cref="ScheduledDaemon.Behaviour"/></param>
        /// <param name="taskManager"><inheritdoc cref="ScheduledDaemon._taskManager"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="ScheduledDaemon._calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="ScheduledDaemon._intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="ScheduledDaemon._hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="ScheduledDaemon._cache"/></param>
        /// <param name="logger"><inheritdoc cref="ScheduledDaemon._logger"/></param>
        public AutoManagedRecurringJobWorkerSwarmHost(AutoCreateRecurringJobWorkerSwarmHostOptions autoManagedOptions, IRecurringJobClient client, string queueType, IOptionsMonitor<RecurringJobWorkerSwarmDefaultHostOptions> defaultOptions, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
        : base(new RecurringJobWorkerSwarmHostOptions() { Name = "AutoManaged" }, client, queueType, defaultOptions, jobQueueProvider, schedulerProvider, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _options = Guard.IsNotNull(autoManagedOptions);
            AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile.Instance.Validate(_options).ThrowOnValidationErrors();
        }

        public override async Task Execute(IDaemonExecutionContext context, CancellationToken token)
        {
            context = Guard.IsNotNull(context);

            context.Log($"Auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> starting. Configuring", context.Daemon.Name);

            while (!token.IsCancellationRequested)
            {
                if (_autoManaged)
                {
                    await AutoConfigureOptionsIfChanged(context, false, token).ConfigureAwait(false);

                    using(var cancellationSource = new CancellationTokenSource())
                    {
                        using var tokenRegistration = token.Register(cancellationSource.Cancel);    
                        using (await _taskManager.ScheduleRecurringActionAsync(this, "MonitorAutomanagedOptionChanges", false, _options.QueuePollingInterval, async t =>
                        {
                            if(!_autoManaged)
                            {
                                cancellationSource.Cancel();
                                return;
                            }
                            context.Log($"Checking if options need to be changed for auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}>", context.Daemon.Name);

                            await AutoConfigureOptionsIfChanged(context, true, t).ConfigureAwait(false);
                        }, (e, t) =>
                        {
                            context.Log(LogLevel.Error, $"Error while monitoring option changes for auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}>", context.Daemon.Name, e);
                            return Task.FromResult(true);
                        }, null, false, cancellationSource.Token).ConfigureAwait(false))
                        {
                            await base.Execute(context, cancellationSource.Token).ConfigureAwait(false);
                        }
                    }
                    
                }
                else
                {
                    context.Log(LogLevel.Warning, $"Options for auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> were modified manually. Running normally without auto managing the options", context.Daemon.Name);
                    await base.Execute(context, token).ConfigureAwait(false);
                }
            }
        }

        private async Task AutoConfigureOptionsIfChanged(IDaemonExecutionContext context, bool restart, CancellationToken token)
        {
            context = Guard.IsNotNull(context);

            bool needsRestart = false;

            // Check drones
            var drones = _options.Drones ?? (int)Math.Floor(Environment.ProcessorCount * _options.AutoManagedDroneCoreMultiplier);

            if(drones != Options.Drones)
            {
                context.Log(LogLevel.Debug, $"Auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> will use <{drones}> drones", context.Daemon.Name);
                needsRestart = true;
            }
            else
            {
                context.Log(LogLevel.Debug, $"Drones for auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> already set correctly. No restart needed", context.Daemon.Name);
            }

            // Check queues
            context.Log(LogLevel.Debug, $"Checking queues for auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}>", context.Daemon.Name);
            var rootQueueGroup = SwarmState?.Scheduler?.Queues?.CurrentQueueGroups.FirstOrDefault();
            var newQueues = new List<string>();

            if (_options.QueueSubscriptions.HasValue())
            {
                await using (var connection = await _client.OpenConnectionAsync(context.Daemon.Colony.Environment, false, token).ConfigureAwait(false))
                {
                    foreach (var queueSubscription in _options.QueueSubscriptions)
                    {
                        var queues = await _client.GetAllQueuesAsync(connection, queueSubscription.Prefix, token).ConfigureAwait(false);

                        if(rootQueueGroup != null)
                        {
                            var missingQueues = queues.Where(x => !rootQueueGroup.SourceQueues.Contains(x, StringComparer.OrdinalIgnoreCase));

                            if (missingQueues.HasValue())
                            {
                                context.Log(LogLevel.Debug, $"Auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> not working on new queues <{missingQueues.JoinString("|")}>. Swarm will be restarted", context.Daemon.Name);
                                needsRestart = true;
                                newQueues = queues.ToList();
                            }
                        }
                        else
                        {
                            needsRestart = true;
                            newQueues = queues.ToList();
                        }
                    }
                }
            }
            else
            {
                string[] queues;
                await using (var connection = await _client.OpenConnectionAsync(context.Daemon.Colony.Environment, false, token).ConfigureAwait(false))
                {
                    queues = await _client.GetAllQueuesAsync(connection, null, token).ConfigureAwait(false);
                }

                if (rootQueueGroup != null)
                {
                    var missingQueues = queues.Where(x => !rootQueueGroup.SourceQueues.Contains(x, StringComparer.OrdinalIgnoreCase));

                    if (missingQueues.HasValue())
                    {
                        context.Log(LogLevel.Debug, $"Auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> not working on new queues <{missingQueues.JoinString("|")}>. Swarm will be restarted", context.Daemon.Name);
                        needsRestart = true;
                        newQueues = queues.ToList();
                    }
                }
                else
                {
                    needsRestart = true;
                    newQueues = queues.ToList();
                }
            }

            if (needsRestart)
            {
                context.Log($"Auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> not running with wanted options. Setting options to trigger restart", context.Daemon.Name);

                UpdateOptions(new RecurringJobWorkerSwarmHostOptions()
                {
                    Name = "AutoManaged",
                    Drones = drones,
                    Queues = newQueues.Select(x => new SwarmQueue()
                    {
                        Name = x,
                        Priority = _options.QueueSubscriptions.HasValue() ? _options.QueueSubscriptions.FirstOrDefault(s => x.StartsWith(s.Prefix, StringComparison.OrdinalIgnoreCase))?.Priority : null
                    }).ToList()
                }, restart);
            }
            else
            {
                context.Log(LogLevel.Debug, $"Auto managed recurring job worker swarm host <{HiveLog.Daemon.NameParam}> already running with correct options. No restart needed", context.Daemon.Name);
            }
        }

        /// <inheritdoc/>
        public override void SetOptions(RecurringJobWorkerSwarmHostOptions options)
        {
            options = Guard.IsNotNull(options);

            if(_options != null) // Don't trigger in ctor call by base class
            {
                _logger.Warning($"Options on auto managed recurring job worker swarm host were manually updated. Not automanaging anymore");
                _autoManaged = false;
            }

            base.SetOptions(options);
        }
    }
}
