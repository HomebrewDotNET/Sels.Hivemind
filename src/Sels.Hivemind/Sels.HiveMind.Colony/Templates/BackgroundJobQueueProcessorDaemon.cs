using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Async.Queue;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Validation;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Extensions;
using Sels.HiveMind.Colony.Validation;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Helper;

namespace Sels.HiveMind.Colony.Templates
{
    /// <summary>
    /// Base class for creating a daemon that locks background jobs matching a certain criteria and processes them in bulk.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the daemon</typeparam>
    /// <typeparam name="TOptionsValidator">The type of validator to use for <typeparamref name="TOptions"/></typeparam>
    public abstract class BackgroundJobQueueProcessorDaemon<TOptions, TOptionsValidator> : OptionsScheduledDaemon<TOptions, TOptionsValidator>
        where TOptions :  IBackgroundJobQueueProcessorOptions
        where TOptionsValidator : BackgroundJobQueueProcessingOptionsValidationProfile
    {
        // Fields
        private readonly object _lock = new object();
        /// <summary>
        /// Client used to manage background jobs.
        /// </summary>
        protected readonly IBackgroundJobClient _client;

        // State
        private TaskCompletionSource<bool> _sleepSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc cref="BackgroundJobQueueProcessorDaemon{TOptions, TOptionsValidator}"/>
        /// <param name="options">The options to use for the current instance</param>
        /// <param name="client"><inheritdoc cref="_client"/></param>
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
        protected BackgroundJobQueueProcessorDaemon(TOptions options, IBackgroundJobClient client, TOptionsValidator optionsValidationProfile, Action<IScheduleBuilder> scheduleBuilder, ScheduleDaemonBehaviour scheduleBehaviour, ITaskManager taskManager, IIntervalProvider intervalProvider, ICalendarProvider calendarProvider, ScheduleValidationProfile validationProfile, IOptionsMonitor<HiveMindOptions> hiveOptions, IMemoryCache? cache = null, ILogger? logger = null) 
            : base(options, optionsValidationProfile, scheduleBuilder, scheduleBehaviour, false, taskManager, intervalProvider, calendarProvider, validationProfile, hiveOptions, cache, logger)
        {
            _client = client.ValidateArgument(nameof(client));
        }

        /// <inheritdoc/>
        public override async Task Execute(IDaemonExecutionContext context, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            context.Log($"Preparing to check queue for new jobs to process");
            var drones = Options.Drones ?? Math.Floor(Environment.ProcessorCount * Options.AutoManagedDroneCoreMultiplier).ConvertTo<int>();
            drones = drones <= 0 ? 1 : drones;
            context.Log(LogLevel.Debug, $"Daemon will use <{drones}> to process the dequeued jobs");
            await using var queue = new WorkerQueue<ILockedBackgroundJob>(_taskManager, _logger);

            var stopSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var requestMonitor = queue.InterceptRequest(c =>
            {
                TriggerDequeue();

                return Task.FromResult<ILockedBackgroundJob?>(null);
            });
            var earlyFetchThreshold = Math.Floor(Options.BatchSize * drones * Options.EarlyFetchThreshold).ConvertTo<uint>();
            using var queueMonitor = queue.OnQueueBelow(earlyFetchThreshold, t =>
            {
                TriggerDequeue();
                return Task.CompletedTask;
            });
            await _taskManager.ScheduleActionAsync(queue, "JobFetcher", false, async (t) => await FetchJobsUntilCancellation(context, queue, stopSource, t), x => x.WithPolicy(NamedManagedTaskPolicy.CancelAndStart),token).ConfigureAwait(false);
            using var droneSubscription = queue.Subscribe(drones, (j, t) => ProcessJobs(context, queue, j, t), token);

            context.Log($"Started <{drones}> drones to process any pending jobs. Waiting until queue is empty or daemon gets cancelled");
            await Helper.Async.WaitOn(stopSource.Task, token).ConfigureAwait(false);
            context.Log($"Queue empty or daemon was requested to stop. Stopping");
        }

        private async Task FetchJobsUntilCancellation(IDaemonExecutionContext context, WorkerQueue<ILockedBackgroundJob> queue, TaskCompletionSource<bool> stopSource, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            queue.ValidateArgument(nameof(queue));
            stopSource.ValidateArgument(nameof(stopSource));

            using var logScope = _logger.TryBeginScope(context);
            try
            {
                bool isFirstBatch = true;
                while(!token.IsCancellationRequested)
                {
                    context.Log($"Fetching the next <{Options.DequeueSize}> background jobs to process");

                    var result = await _client.SearchAndLockAsync(x => SetSearchCriteria(context, x), Options.DequeueSize, context.Daemon.FullyQualifiedName, false, token: token).ConfigureAwait(false);

                    if(!isFirstBatch && result.Results.Count < Options.MininumBatchSize)
                    {
                        context.Log($"Fetched <{result.Results.Count}> background jobs which is below the minimum batch size of <{Options.MininumBatchSize}>. Returning jobs to queue and stopping");
                        stopSource.TrySetResult(true);
                        await result.DisposeAsync().ConfigureAwait(false);                  
                        return;
                    }
                    else
                    {
                        context.Log($"Adding <{result.Results.Count}> background jobs to local queue");

                        foreach(var job in result.Results)
                        {
                            await queue.EnqueueAsync(job).ConfigureAwait(false);
                        }
                    }
                    isFirstBatch = false;

                    if(result.Results.Count <= 0)
                    {
                        context.Log($"Queue empty. Stopping");
                        stopSource.TrySetResult(true);
                        await result.DisposeAsync().ConfigureAwait(false);
                        return;
                    }

                    context.Log(LogLevel.Debug, "Fetcher sleeping until woken up again");

                    Task sleepTask = null;
                    lock (_lock)
                    {
                        sleepTask = _sleepSource.Task;
                    }

                    await Helper.Async.WaitOn(sleepTask, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when(token.IsCancellationRequested)
            {
                context.Log("Fetch was cancelled");
            }
            catch(Exception ex)
            {
                context.Log("Something went wrong while trying to fetch background jobs to process", ex);
                return;
            }
            finally
            {
                stopSource.TrySetResult(true);
            }
        }

        private async Task ProcessJobs(IDaemonExecutionContext context, WorkerQueue<ILockedBackgroundJob> queue, ILockedBackgroundJob lockedBackgroundJob, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            queue.ValidateArgument(nameof(queue));
            lockedBackgroundJob.ValidateArgument(nameof(lockedBackgroundJob));
            using var logScope = _logger.TryBeginScope(context);

            context.Log("Drone starting the processing of the next batch");

            List<ILockedBackgroundJob> jobs = new List<ILockedBackgroundJob>();
            await lockedBackgroundJob.EnsureValidLockAsync(token).ConfigureAwait(false);
            jobs.Add(lockedBackgroundJob);

            // Add artificial delay to allow the fetcher to fill the queue
            await Helper.Async.Sleep(TimeSpan.FromMilliseconds(2 * Options.BatchSize), token).ConfigureAwait(false);

            try
            {
                if (token.IsCancellationRequested) return;
                context.Log(LogLevel.Debug, $"Checking queue until we have <{Options.BatchSize}> jobs");

                while(jobs.Count < Options.BatchSize && await queue.TryDequeueAsync(token).ConfigureAwait(false) is (true, var dequeuedJob))
                {
                    token.ThrowIfCancellationRequested();
                    context.Log(LogLevel.Debug, $"Dequeued job <{HiveLog.Job.IdParam}>. Ensuring valid lock and adding to drone queue", dequeuedJob.Id);
                    try
                    {
                        await dequeuedJob.EnsureValidLockAsync(token).ConfigureAwait(false);
                        jobs.Add(dequeuedJob);
                    }
                    catch(Exception ex)
                    {
                        context.Log($"Something went wrong while ensuring valid lock on dequeued job <{HiveLog.Job.IdParam}>. Returning to queue", ex, dequeuedJob.Id);
                        await dequeuedJob.DisposeAsync().ConfigureAwait(false);
                    }
                }

                context.Log($"Drone starting the processing batch of <{jobs.Count}> jobs");
                await ProcessJobsAsync(context, jobs, token).ConfigureAwait(false);
                context.Log($"Drone finished processing batch of <{jobs.Count}> jobs");
            }
            catch (OperationCanceledException)
            {
                context.Log(LogLevel.Debug, $"Drone was cancelled");
            }
            catch(Exception ex)
            {
                context.Log("Something went wrong while processing background jobs", ex);
                return;
            }
            finally
            {
                await jobs.ForceExecuteAsync(async x => await x.DisposeAsync(), (x, e) => context.Log($"Something went wrong while disposing <{x}>", e)).ConfigureAwait(false);               
            }
        }

        /// <summary>
        /// Configures the search criteria for the background jobs that should be processed.
        /// </summary>
        /// <param name="context">The context of the executing daemon</param>
        /// <param name="builder">The builder to use to configure the search criteria</param>
        /// <returns>The last result returned by <paramref name="builder"/></returns>
        protected abstract IChainedQueryConditionBuilder<IQueryJobConditionBuilder> SetSearchCriteria(IDaemonExecutionContext context, IQueryJobConditionBuilder builder);

        /// <summary>
        /// Processes <paramref name="jobs"/>. Dispose is handled already.
        /// </summary>
        /// <param name="context">The context of the executing daemon</param>
        /// <param name="jobs">The jobs to process</param>
        /// <param name="token">Token that will be cancelled when the job is requested to stop</param>
        protected abstract Task ProcessJobsAsync(IDaemonExecutionContext context, List<ILockedBackgroundJob> jobs, CancellationToken token);

        private void TriggerDequeue()
        {
            lock (_lock)
            {
                _sleepSource.TrySetResult(true);
                _sleepSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
