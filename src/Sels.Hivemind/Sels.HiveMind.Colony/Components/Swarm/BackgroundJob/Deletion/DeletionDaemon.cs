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
using Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker;
using Sels.HiveMind.Colony.Swarm.BackgroundJob.Deletion;
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

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Deletion
{
    /// <summary>
    /// A swarm host that is responsible for system deleting background jobs.
    /// </summary>
    public class DeletionDaemon : BackgroundJobSwarmHost<DeletionDeamonOptions, DeletionDaemonDefaultOptions>
    {
        // Fields
        private readonly object _lock = new object();

        // State
        private DeletionDeamonOptions _currentOptions;
        private CancellationTokenSource _reloadSource;

        // Properties
        /// <inheritdoc/>
        protected override DeletionDeamonOptions Options { get { lock (_lock) { return _currentOptions; } } }
        /// <summary>
        /// The current options being used by the swarm host.
        /// </summary>
        public IDeletionDaemonOptions CurrentOptions => Options;
        /// <inheritdoc/>
        protected override string SwarmPrefix => $"Deletion.";

        /// <inheritdoc cref="WorkerSwarmHost"/>
        /// <param name="defaultWorkerOptions">The default worker options for this swarm</param>
        /// <param name="defaultOptions"><inheritdoc cref="_defaultOptions"/></param>
        /// <param name="taskManager">Used to manage dromes</param>
        /// <param name="jobQueueProvider">Used to resolve the job queue</param>
        /// <param name="schedulerProvider">Used to create schedulers for the swarms</param>
        public DeletionDaemon(DeletionDeamonOptions options, IOptionsMonitor<DeletionDaemonDefaultOptions> defaultOptions, ITaskManager taskManager, IJobQueueProvider jobQueueProvider, IJobSchedulerProvider schedulerProvider) : base(HiveMindConstants.Queue.BackgroundJobCleanupQueueType, defaultOptions, taskManager, jobQueueProvider, schedulerProvider)
        {
            SetOptions(options);
        }

        /// <summary>
        /// Overwrites the options for this instance. Host will restart.
        /// </summary>
        /// <param name="options"><inheritdoc cref="Options"/></param>
        public void SetOptions(DeletionDeamonOptions options)
        {
            options.ValidateArgument(nameof(options));
            options.ValidateAgainstProfile<DeletionDeamonOptionsValidationProfile, DeletionDeamonOptions, string>().ThrowOnValidationErrors();

            lock (_lock)
            {
                _currentOptions = options;
                Reload();
            }
        }

        private void Reload()
        {
            lock(_lock)
            {
                if (_reloadSource != null) _reloadSource.Cancel();
                _reloadSource = new CancellationTokenSource();
            }
        }

        /// <inheritdoc/>
        public override async Task RunAsync(IDaemonExecutionContext context, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var swarmTokenSource = new CancellationTokenSource();

                context.Log($"Deletion daemon <{HiveLog.Daemon.Name}> setting up", context.Daemon.Name);

                try
                {
                    // Monitoring for changes and restart if changes are detected
                    using var tokenSourceRegistration = token.Register(() => swarmTokenSource.Cancel());
                    using var defaultOptionsMonitorRegistration = _defaultOptions.OnChange((o, n) =>
                    {
                        if (n.EqualsNoCase(context.Daemon.Colony.Environment)) swarmTokenSource.Cancel();
                    });
                    CancellationTokenSource reloadSource;
                    lock (_lock)
                    {
                        reloadSource = _reloadSource;
                    }
                    using var reloadSourceRegistration = reloadSource.Token.Register(() => swarmTokenSource.Cancel());

                    if (Options.IsAutoManaged)
                    {
                        await SetAutoManagedOptions(context, swarmTokenSource.Token).ConfigureAwait(false);
                        swarmTokenSource.CancelAfter(Options.AutoManagedRestartInterval ?? _defaultOptions.CurrentValue.AutoManagedRestartInterval);
                    }
                    if (swarmTokenSource.IsCancellationRequested) continue;
                    await base.RunAsync(context, swarmTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    context.Log(LogLevel.Debug, $"Deletion daemon <{HiveLog.Daemon.Name}> cancelled", context.Daemon.Name);
                    continue;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private async Task SetAutoManagedOptions(IDaemonExecutionContext context, CancellationToken token)
        {
            context.Log($"Deletion daemon <{HiveLog.Daemon.Name}> is auto managed. Setting options", context.Daemon.Name);

            context.Log(LogLevel.Debug, $"Deletion daemon <{HiveLog.Daemon.Name}> querying active queues", context.Daemon.Name);
            var client = context.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            await using var connection = await client.OpenConnectionAsync(context.Daemon.Colony.Environment, false, token).ConfigureAwait(false);
            var currentOptions = Options;

            //// Determine queues to delete from
            // Get all active queues on persisted background jobs
            var queuesTask = client.GetAllQueuesAsync(connection, token);

            var activeQueues = new HashSet<string>();
            // Check the config of other workers swarms so they the queues they are listening on
            foreach(var daemon in context.Daemon.Colony.Daemons)
            {
                if(daemon.Instance is WorkerSwarmHost workerSwarmHost)
                {
                    foreach(var queue in GetAllQueues(workerSwarmHost.CurrentOptions))
                    {
                        activeQueues.Add(queue);
                    }
                }
            }
            activeQueues.Intersect(await queuesTask.ConfigureAwait(false));
            activeQueues.Add(HiveMindConstants.Queue.DefaultQueue);

            context.Log(LogLevel.Debug, $"Deletion daemon <{HiveLog.Daemon.Name}> found <{activeQueues.Count}> active queues", context.Daemon.Name);
            currentOptions.SubSwarmOptions = null; // We only configure the root swarm
            currentOptions.WorkOnGlobalQueue = false;
            activeQueues.Execute(x => currentOptions.AddQueue(x));

            //// Determine amount of workers
            currentOptions.Drones = Math.Floor(Environment.ProcessorCount * (currentOptions.AutoManagedDroneCoreMultiplier ?? _defaultOptions.CurrentValue.AutoManagedDroneCoreMultiplier)).ConvertTo<int>();
            if (currentOptions.Drones.HasValue && currentOptions.Drones.Value <= 0) currentOptions.Drones = 1;

            context.Log($"Auto managed deletion daemon <{HiveLog.Daemon.Name}> will work on <{currentOptions.Queues?.Count ?? 0}> queues using <{currentOptions.Drones}> drones", context.Daemon.Name);

            // Set options
            lock (_lock)
            {
                // If options are not the same they might have been updated so reload
                if(currentOptions != _currentOptions)
                {
                    Reload();
                    return;
                }

                _currentOptions = currentOptions;
            }
        }

        private IEnumerable<string> GetAllQueues(IWorkerSwarmHostOptions options)
        {
            options.ValidateArgument(nameof(options));

            if (options.Queues.HasValue())
            {
                foreach(var queue in options.Queues.Select(x => x.Name))
                {
                    yield return queue;
                }
            }

            if (options.SubSwarmOptions.HasValue())
            {
                foreach(var option in options.SubSwarmOptions)
                {
                    foreach(var queue in GetAllQueues(option))
                    {
                        yield return queue;
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override bool CanBeProcessed(IDaemonExecutionContext context, IDroneState<DeletionDeamonOptions> state, IDequeuedJob job, IReadOnlyBackgroundJob backgroundJob, HiveMindOptions options, out TimeSpan? delay)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));
            options.ValidateArgument(nameof(options));

            delay = null;

            // Execution matches
            if (!job.ExecutionId.Equals(backgroundJob.ExecutionId))
            {
                context.Log(LogLevel.Warning, $"Execution id of dequeued job does not match execution id of background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}>. Can't process", backgroundJob.Id, backgroundJob.Environment);
                return false;
            }

            // Check in valid state
            if (!backgroundJob.State.Name.In(options.CompletedBackgroundJobStateNames))
            {
                context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> is not in a valid state to be deleted. Current state is <{HiveLog.BackgroundJob.State}> which is not in <{options.CompletedBackgroundJobStateNames.JoinString(", ")}>", backgroundJob.Id, backgroundJob.Environment, backgroundJob.State.Name);
                return false;
            }

            // Check retention
            if (!options.CompletedBackgroundJobRetention.HasValue)
            { 
                context.Log(LogLevel.Warning, $"No retention is configured for environment <{HiveLog.Environment}>. Background job {HiveLog.Job.Id} won't be deleted", backgroundJob.Environment, backgroundJob.Id);
                return false;
            }

            // Check deviation
            var expectedDeleteDate = backgroundJob.State.ElectedDateUtc.Add(options.CompletedBackgroundJobRetention.Value);
            var now = DateTime.UtcNow;
            if(now < (expectedDeleteDate.Add(-(Options.AllowedDeviation ?? _defaultOptions.CurrentValue.AllowedDeviation))))
            {
                delay = (expectedDeleteDate - now);
                context.Log(LogLevel.Warning, $"Background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> was scheduled for deletion too soon. Job will be delayed by <{delay}>", backgroundJob.Environment, backgroundJob.Id);
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override async Task ProcessJobAsync(IDaemonExecutionContext context, IDroneState<DeletionDeamonOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, CancellationTokenSource jobCancellationSource, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            state.ValidateArgument(nameof(state));
            job.ValidateArgument(nameof(job));
            backgroundJob.ValidateArgument(nameof(backgroundJob));
            options.ValidateArgument(nameof(options));

            await backgroundJob.SystemDeleteAsync(token);
            context.Log($"Drone <{HiveLog.Swarm.DroneName}> successfully deleted background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}>", state.FullName, backgroundJob.Id, backgroundJob.Environment);
            await job.CompleteAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override Task HandleErrorAsync(IDaemonExecutionContext context, IDroneState<DeletionDeamonOptions> state, IDequeuedJob job, ILockedBackgroundJob backgroundJob, HiveMindOptions options, Exception exception, CancellationToken token)
        {
            return job.DelayToAsync(DateTime.UtcNow.Add(Options.ErrorDelay ?? _defaultOptions.CurrentValue.ErrorDelay), token);   
        }
    }
}
