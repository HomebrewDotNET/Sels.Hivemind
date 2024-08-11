using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Validation;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Colony.Options;
using Sels.HiveMind.Colony.Validation;
using Sels.Core.Mediator;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Events.Job;

namespace Sels.HiveMind.Colony.SystemDaemon
{
    /// <summary>
    /// A swarm host that is responsible for system deleting background jobs.
    /// </summary>
    public class DeletionDaemon : BackgroundJobQueueProcessorDaemon<DeletionDeamonOptions>
    {
        // Fields
        private readonly object _lock = new object();
        private readonly INotifier _notifier;

        // State
        private long _deleted;
        private int _deleting;
        private int _activeDrones;

        // Properties
        /// <inheritdoc/>
        protected object DaemonState => new DeletionDaemonState()
        {
            Deleted = _deleted,
            Deleting = _deleting,
            ActiveDrones = _activeDrones,
            DaemonState = State
        };

        /// <inheritdoc cref="WorkerSwarmHost"/>
        /// <param name="notifier">Used to raise events</param>
        /// <param name="options"><inheritdoc cref="BackgroundJobQueueProcessorDaemon{TOptions}.Options"/></param>
        /// <param name="client"><inheritdoc cref="BackgroundJobQueueProcessorDaemon{TOptions}._client"/></param>
        /// <param name="optionsValidationProfile">Used to validate the options</param>
        /// <param name="scheduleBuilder"><inheritdoc cref="ScheduledDaemon.Schedule"/></param>
        /// <param name="scheduleBehaviour"><inheritdoc cref="ScheduledDaemon.Behaviour"/></param>
        /// <param name="taskManager"><inheritdoc cref="ScheduledDaemon._taskManager"/></param>
        /// <param name="calendarProvider"><inheritdoc cref="ScheduledDaemon._calendarProvider"/></param>
        /// <param name="intervalProvider"><inheritdoc cref="ScheduledDaemon._intervalProvider"/></param>
        /// <param name="validationProfile">Used to validate the schedules</param>
        /// <param name="hiveOptions"><inheritdoc cref="ScheduledDaemon._hiveOptions"/></param>
        /// <param name="cache"><inheritdoc cref="ScheduledDaemon._cache"/></param>
        /// <param name="logger"><inheritdoc cref="ScheduledDaemon._logger"/></param>
        public DeletionDaemon(INotifier notifier, DeletionDeamonOptions options, IBackgroundJobClient client, DeletionDeamonOptionsValidationProfile optionsValidationProfile, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache cache = null, ILogger logger = null) : base(options, client, optionsValidationProfile, scheduleBuilder, scheduleBehaviour, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _notifier = notifier.ValidateArgument(nameof(notifier));
            SetOptions(options);
        }

        /// <inheritdoc/>
        public override async Task Execute(IDaemonExecutionContext context, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            context.StateGetter = () => DaemonState;
            var options = _hiveOptions.Get(context.Daemon.Colony.Environment);
            if (!options.CompletedBackgroundJobRetention.HasValue)
            {
                context.Log(LogLevel.Warning, $"Deletion daemon was started but no retention was configured for the environment. Daemon will sleep until cancellation");

                await Helper.Async.WaitUntilCancellation(token).ConfigureAwait(false);
            }
            else
            {
                await base.Execute(context, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        protected override async Task ProcessJobsAsync(IDaemonExecutionContext context, List<ILockedBackgroundJob> jobs, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            jobs.ValidateArgument(nameof(jobs));

            using var inProcessScope = new InProcessAction(x =>
            {
                lock (_lock)
                {
                    if (x)
                    {
                        _activeDrones++;
                    }
                    else
                    {
                        _activeDrones--;
                    }
                }
            });
            context.Log($"Deletion daemon starting deletion of <{jobs.Count}> background jobs");
            var initialCount = jobs.Count;

            try
            {
                lock (_lock)
                {
                    _deleting += jobs.Count;
                }

                await _notifier.RaiseEventAsync(this, new SystemDeletingBackgroundJobsEvent(jobs), token).ConfigureAwait(false);

                foreach (var job in jobs.ToArray())
                {
                    await job.EnsureValidLockAsync(token).ConfigureAwait(false);
                    if (job.TryGetProperty<bool>(HiveMindConstants.Job.Properties.MarkedForDeletion, out var markedForDeletion) && markedForDeletion)
                    {
                        context.Log(LogLevel.Debug, $"Changing state of background job <{HiveLog.Job.IdParam}> to <{HiveLog.Job.StateParam}>", job.Id, SystemDeletingState.StateName);
                        
                        if (!await job.ChangeStateAsync(new SystemDeletingState() { Reason = "Triggered by deletion daemon" }, token).ConfigureAwait(false))
                        {
                            context.Log(LogLevel.Warning, $"Could not change state on background job <{HiveLog.Job.IdParam}> to deleting state. New state is <{HiveLog.Job.StateParam}>", job.Id, job.State.Name);
                            jobs.Remove(job);
                            await job.SaveChangesAsync(false, token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        context.Log(LogLevel.Warning, $"Background job <{HiveLog.Job.IdParam}> is no longer marked for deletion", job.Id);
                        jobs.Remove(job);
                        await job.SaveChangesAsync(false, token).ConfigureAwait(false);
                    }
                }

                if (initialCount != jobs.Count)
                {
                    lock (_lock)
                    {
                        _deleting -= initialCount - jobs.Count;
                    }
                    initialCount = jobs.Count;
                }

                if (jobs.Count == 0)
                {
                    context.Log(LogLevel.Warning, "No more background jobs to delete because they all timed out");
                    return;
                }

                context.Log(LogLevel.Debug, $"Opening transaction to delete <{jobs.Count}> background jobs");

                await using (var connection = await _client.OpenConnectionAsync(context.Daemon.Colony.Environment, true, token).ConfigureAwait(false))
                {
                    var deletedIds = await connection.StorageConnection.Storage.TryDeleteBackgroundJobsAsync(jobs.Select(x => x.Id).ToArray(), context.Daemon.FullyQualifiedName, connection.StorageConnection, token).ConfigureAwait(false);

                    foreach (var job in jobs.ToArray())
                    {
                        try
                        {
                            if (deletedIds.Contains(job.Id, StringComparer.OrdinalIgnoreCase))
                            {
                                context.Log(LogLevel.Debug, $"Background job <{HiveLog.Job.IdParam}> was deleted. Trying to set system deleted state");

                                if (!await job.SetSystemDeletedAsync(connection.StorageConnection, "Triggered by deletion daemon", token).ConfigureAwait(false))
                                {
                                    context.Log(LogLevel.Error, $"Could not set system deleted state on background job <{HiveLog.Job.IdParam}>. Aborting transaction", job.Id);
                                    await connection.AbortTransactionAsync(token).ConfigureAwait(false);
                                    return;
                                }
                                else
                                {
                                    context.Log($"Background job <{HiveLog.Job.IdParam}> was moved to system deleted state", job.Id);
                                }
                            }
                            else
                            {
                                context.Log(LogLevel.Warning, $"Background job <{HiveLog.Job.IdParam}> couldn't be deleted. Returing to queue", job.Id);
                                jobs.Remove(job);
                                await job.DisposeAsync().ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Log($"Error deleting background job <{HiveLog.Job.IdParam}>", ex, job.Id);
                            throw;
                        }
                    }

                    await _notifier.RaiseEventAsync(this, new SystemDeletedBackgroundJobsEvent(jobs, connection.StorageConnection), token).ConfigureAwait(false);
                    context.Log(LogLevel.Debug, "Commiting transaction");
                    await connection.CommitAsync(token).ConfigureAwait(false);
                }

                context.Log($"Deletion daemon deleted <{jobs.Count}> background jobs");

                lock (_lock)
                {
                    _deleted += jobs.Count;
                }
            }
            finally
            {
                lock (_lock)
                {
                    _deleting -= initialCount;
                }
            }
        }
        /// <inheritdoc/>
        protected override IChainedQueryConditionBuilder<IQueryJobConditionBuilder> SetSearchCriteria(IDaemonExecutionContext context, IQueryJobConditionBuilder builder) => builder.Property(HiveMindConstants.Job.Properties.MarkedForDeletion).AsBool.EqualTo(true).And
                                                                                                                                                                                    .CurrentState.ElectedDate.LesserOrEqualTo(DateTime.UtcNow.Add(-_hiveOptions.Get(context.Daemon.Colony.Environment).CompletedBackgroundJobRetention.Value));
        private class DeletionDaemonState
        {
            public long Deleted { get; set; }
            public int Deleting { get; set; }
            public int ActiveDrones { get; set; }
            public ScheduledDaemonState DaemonState { get; set; }

            public override string ToString()
            {
                return $"Deleted: {Deleted} | Deleting: {Deleting} | Active Drones: {ActiveDrones} ({DaemonState})";
            }
        }
    }
}
