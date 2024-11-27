using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Scope;
using Sels.Core.Scope.Actions;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Options;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.DistributedLocking;
using Sels.HiveMind.Events.Job.Background;
using Sels.HiveMind.Extensions;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Sels.HiveMind.Colony.SystemDaemon
{
    /// <summary>
    /// A swarm host that is responsible for system deleting background jobs when <see cref="DeletionMode"/> is to <see cref="DeletionMode.Bulk"/>.
    /// </summary>
    public class BulkDeletingDeletionDaemon : OptionsScheduledDaemon<DeletionDeamonOptions, DeletionDeamonOptionsValidationProfile>
    {
        // Fields
        private readonly object _lock = new object();
        private readonly INotifier _notifier;
        private readonly IBackgroundJobClient _client;
        private readonly IDistributedLockServiceProvider _distributedLockServiceProvider;

        // State
        private long _deleted;
        private int _deleting;
        private bool _isActive;

        // Properties
        /// <summary>
        /// The current state of the daemon.
        /// </summary>
        protected object DaemonState => new DeletionDaemonState()
        {
            Deleted = _deleted,
            Deleting = _deleting,
            ActiveDrones = _isActive ? 1 : 0,
            DaemonState = State
        };

        /// <inheritdoc cref="SystemDeletingDeletionDaemon"/>
        /// <param name="notifier">Used to raise events</param>
        /// <param name="options"><inheritdoc cref="OptionsScheduledDaemon{TOptions, TOptionsValidator}.Options"/></param>
        /// <param name="client">The client that will be used to delete the jobs</param>
        /// <param name="distributedLockServiceProvider">Used to get a distributed lock when concurrent deletion is enabled</param>
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
        public BulkDeletingDeletionDaemon(INotifier notifier, DeletionDeamonOptions options, IBackgroundJobClient client, IDistributedLockServiceProvider distributedLockServiceProvider, DeletionDeamonOptionsValidationProfile optionsValidationProfile, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null)
            : base(options, optionsValidationProfile, scheduleBuilder, scheduleBehaviour, false, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _notifier = Guard.IsNotNull(notifier);
            _client = Guard.IsNotNull(client);
            _distributedLockServiceProvider = Guard.IsNotNull(distributedLockServiceProvider);
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
                if(Options.MaxConcurrentBulkDeletions > 0)
                {
                    context.Log($"Maximum of <{Options.MaxConcurrentBulkDeletions}> allowed. Trying to acquire distributed lock");

                    var distributedLockServiceScope = await _distributedLockServiceProvider.CreateAsync(context.Daemon.Colony.Environment, token).ConfigureAwait(false);
                    var distributedLockService = distributedLockServiceScope.Component;

                    IDistributedLock? distributedLock = null;
                    foreach(var nodeId in Enumerable.Range(0, Options.MaxConcurrentBulkDeletions))
                    {
                        var resource = $"{HiveMindColonyConstants.DeletionDaemon.DeletionDaemonDistributedLockPrefix}.{nodeId}";
                        context.Log(LogLevel.Debug, $"Trying to acquire distributed lock on <{resource}>");

                        var (wasAcquired, requestedDistributedLock) =  await distributedLockService.TryAcquireAsync(resource, context.Daemon.FullyQualifiedName, token).ConfigureAwait(false);

                        if (wasAcquired)
                        {
                            context.Log($"Acquired lock on <{resource}>. Node will be able to bulk delete jobs");
                            distributedLock = requestedDistributedLock;
                            break;
                        }
                        else
                        {
                            context.Log(LogLevel.Debug, $"Failed to acquire distributed lock on <{resource}>. Trying next node id if available");
                        }
                    }

                    if(distributedLock == null)
                    {
                        context.Log(LogLevel.Warning, $"Failed to acquire distributed lock. Daemon `will retry later according to the configured schedule");
                        return;
                    }

                    using var cancellationSource = new CancellationTokenSource();
                    using var cancellationRegistration = token.Register(() => cancellationSource.Cancel());
                    await using var distributedLockScope = distributedLock.KeepAliveDuringScope(this, "MaintainDeleteNodeLock", _taskManager, options.LockExpirySafetyOffset, () =>
                    {
                        context.Log(LogLevel.Error, $"Lost hold on distributed lock <{distributedLock.Resource}>. Daemon will stop");
                        cancellationSource.Cancel();
                        return Task.CompletedTask;
                    }, _logger, token).ConfigureAwait(false);

                    await DeleteUntilQueueEmptyAsync(context, cancellationSource.Token).ConfigureAwait(false);
                }
                else
                {
                    context.Log($"No limit on concurrent deletions configured. Node will be able to bulk delete jobs");
                    await DeleteUntilQueueEmptyAsync(context, token).ConfigureAwait(false);
                }
            }
        }

        private async Task DeleteUntilQueueEmptyAsync(IDaemonExecutionContext context, CancellationToken token)
        {
            context = Guard.IsNotNull(context);
            var environment = context.Daemon.Colony.Environment;

            context.Log($"Daemon checking if there are background jobs that can be deleted");

            var batchSize = Options.BulkDeletionBatchSize;

            using (new InProcessAction(x => _isActive = x))
            {
                int deleted = 0;
                do
                {
                    await using var connection = await _client.OpenConnectionAsync(environment, false, token).ConfigureAwait(false);
                    var options = _hiveOptions.Get(environment);
                    var retention = options.CompletedBackgroundJobRetention;
                    if (!retention.HasValue)
                    {
                        context.Log(LogLevel.Warning, "No retention configured but was expected. Stopping");
                        return;
                    }

                    context.Log($"Deleting the next batch of <{batchSize}> background jobs marked for deletion in the retention window of <{retention}>");
                        await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                        try
                        {
                            var deletedIds = await _client.DeleteAsync(connection, batchSize, x => x.Property<bool>(HiveMindConstants.Job.Properties.MarkedForDeletion).EqualTo(true).And
                                                                                                 .CurrentState(x => x.ElectedDate.LesserOrEqualTo(DateTime.UtcNow.Add(-retention.Value))));
                            using (new ScopedAction(() => _deleting = deletedIds.Length, () => _deleting = 0))
                            {
                                if (deletedIds.Length <= 0)
                                {
                                    context.Log($"No background jobs to delete. Stopping");
                                    return;
                                }

                                context.Log(LogLevel.Debug, $"Deleted <{deletedIds}> background jobs. Raising event");
                                _ = await _notifier.RaiseEventAsync(this, new BulkDeletedBackgroundJobsEvent(deletedIds, connection.StorageConnection)).ConfigureAwait(false);

                                context.Log(LogLevel.Debug, $"Commiting deletion of <{deletedIds.Length}> background jobs");
                                await connection.CommitAsync(token).ConfigureAwait(false);

                                deleted = deletedIds.Length;
                                _deleted += deleted;
                                context.Log($"Deleted <{deleted}> background jobs");
                            }
                        }
                        catch (Exception)
                        {
                            await connection.AbortTransactionAsync(token).ConfigureAwait(false);
                            throw;
                        }                      
                }
                while (deleted >= batchSize);
            }
        }
    }
}
