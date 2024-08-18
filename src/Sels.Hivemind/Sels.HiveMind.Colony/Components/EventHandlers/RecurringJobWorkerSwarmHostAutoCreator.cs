using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Swarm.Job.Background;
using Sels.HiveMind.Colony.Swarm.Job.Recurring;
using Sels.HiveMind.Colony.SystemDaemon;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.EventHandlers
{
    /// <summary>
    /// Creates a <see cref="AutoManagedRecurringJobWorkerSwarmHost"/> daemon if the option is enabled on the colony.
    /// </summary>
    public class RecurringJobWorkerSwarmHostAutoCreator : IColonyCreatedEventHandler
    {
        // Fields
        private readonly ILogger? _logger;
        private readonly IOptionsMonitor<AutoCreateRecurringJobWorkerSwarmHostOptions> _optionsMonitor;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => null;

        /// <inheritdoc cref="RecurringJobWorkerSwarmHostAutoCreator"/>
        /// <param name="optionsMonitor">Used to access the options to create the auto managed daemon</param>
        /// <param name="logger">Optional logger for tracing</param>
        public RecurringJobWorkerSwarmHostAutoCreator(IOptionsMonitor<AutoCreateRecurringJobWorkerSwarmHostOptions> optionsMonitor, ILogger<RecurringJobWorkerSwarmHostAutoCreator>? logger = null)
        {
            _optionsMonitor = Guard.IsNotNull(optionsMonitor);
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected RecurringJobWorkerSwarmHostAutoCreator() { }

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, ColonyCreatedEvent @event, CancellationToken token)
        {
            context = Guard.IsNotNull(context);
            @event = Guard.IsNotNull(@event);

            var colony = @event.Colony;

            if (colony.Options.CreationOptions.HasFlag(HiveColonyCreationOptions.AutoCreateRecurringJobWorkerSwarmHost))
            {
                var daemonOptions = _optionsMonitor.Get(colony.Environment);
                _logger.Log($"Auto creating recurring job worker swarm <{HiveLog.Daemon.NameParam}> for colony <{HiveLog.Colony.NameParam}>", daemonOptions.Name, colony.Name);
                var existing = colony.Daemons.FirstOrDefault(x => x.InstanceType != null && x.InstanceType.IsAssignableTo<RecurringJobWorkerSwarmHost>());

                if (existing != null)
                {
                    _logger.Warning($"Could not auto create recurring job worker swarm because daemon <{HiveLog.Daemon.NameParam}> already exists", existing.Name);
                    return Task.CompletedTask;
                }

                colony.WithDaemon<AutoManagedRecurringJobWorkerSwarmHost>(daemonOptions.Name, (h, c, t) => h.RunUntilCancellation(c, t), x =>
                {
                    return new AutoManagedRecurringJobWorkerSwarmHost(daemonOptions,
                                               x.GetRequiredService<IRecurringJobClient>(),
                                               HiveMindConstants.Queue.RecurringJobProcessQueueType,
                                               x.GetRequiredService<IOptionsMonitor<RecurringJobWorkerSwarmDefaultHostOptions>>(),
                                               x.GetRequiredService<IJobQueueProvider>(),
                                               x.GetRequiredService<IJobSchedulerProvider>(),
                                               new Action<IScheduleBuilder>(x => { }),
                                               ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule,
                                               x.GetRequiredService<ITaskManager>(),
                                               x.GetRequiredService<IIntervalProvider>(),
                                               x.GetRequiredService<ICalendarProvider>(),
                                               x.GetRequiredService<ScheduleValidationProfile>(),
                                               x.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                               x.GetService<IMemoryCache>(),
                                               x.GetService<ILogger<AutoManagedRecurringJobWorkerSwarmHost>>()
                    );
                }, true, x => x.WithRestartPolicy(DaemonRestartPolicy.Always)
                               .WithProperty(HiveMindColonyConstants.Daemon.IsAutoCreatedProperty, true));
            }

            return Task.CompletedTask;
        }
    }
}
