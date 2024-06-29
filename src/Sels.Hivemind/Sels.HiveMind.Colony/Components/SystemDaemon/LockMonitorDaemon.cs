using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Mediator;
using Sels.HiveMind.Client;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.SystemDaemon
{
    /// <summary>
    /// Daemon that monitors locks on jobs and releases them when they expire.
    /// </summary>
    public class LockMonitorDaemon : IDaemonExecutor
    {
        // Fields
        private readonly INotifier _notifier;
        private readonly IOptionsMonitor<HiveMindOptions> _options;
        private readonly IBackgroundJobClient _backgroundJobClient;

        // Properties
        private State CurrentState { get; } = new State();

        /// <inheritdoc cref="LockMonitorDaemon"/>
        /// <param name="notifier">Used to raise events</param>
        /// <param name="backgroundJobClient">Used to open connections to the storage</param>
        /// <param name="options">Used to retrieve the configured options for the environment</param>
        public LockMonitorDaemon(INotifier notifier, IBackgroundJobClient backgroundJobClient, IOptionsMonitor<HiveMindOptions> options)
        {
            _notifier = notifier.ValidateArgument(nameof(notifier));
            _backgroundJobClient = backgroundJobClient.ValidateArgument(nameof(backgroundJobClient));
            _options = options.ValidateArgument(nameof(options));
        }

        /// <inheritdoc/>
        public async Task RunUntilCancellation(IDaemonExecutionContext context, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));

            context.Log($"Daemon <{HiveLog.Daemon.NameParam}> will monitor timed out locks on jobs", context.Daemon.Name);

            try
            {
                context.StateGetter = () => CurrentState;
                while (!token.IsCancellationRequested)
                {
                    var options = _options.Get(context.Daemon.Colony.Environment);

                    await ReleaseBackgroundJobs(context, options, token).ConfigureAwait(false);

                    var sleepTime = options.LockTimeout;
                    context.Log(LogLevel.Debug, $"Daemon <{HiveLog.Daemon.NameParam}> will sleep for <{sleepTime}> before checking again", context.Daemon.Name);
                    await Helper.Async.Sleep(sleepTime, token).ConfigureAwait(false);
                }
            }
            finally
            {
                context.StateGetter = null;
            }

            context.Log($"Daemon <{HiveLog.Daemon.NameParam}> has stopped monitoring timed out locks on jobs", context.Daemon.Name);
        }

        private async Task ReleaseBackgroundJobs(IDaemonExecutionContext context, HiveMindOptions options, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            options.ValidateArgument(nameof(options));

            context.Log($"Checking if there are timed out background jobs that need to be released in environment <{HiveLog.EnvironmentParam}>", context.Daemon.Colony.Environment);

            var result = await GetTimedoutBackgroundJobs(context, options, token).ConfigureAwait(false);

            while(result.Results.Count > 0)
            {
                context.Log($"Got <{result.Results.Count}> timed out background jobs that need to be released in environment <{HiveLog.EnvironmentParam}>", context.Daemon.Colony.Environment);

                await using (result)
                {
                    foreach(var job in result.Results)
                    {
                        await using (job)
                        {
                            var oldState = job.State.Name;
                            context.Log(LogLevel.Debug, $"Releasing timed out background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> which is in state <{HiveLog.Job.StateParam}>", job.Id, job.Environment, oldState);

                            await _notifier.RaiseEventAsync(this, new BackgroundJobLockTimedOutEvent(job), token).ConfigureAwait(false);

                            await job.SaveChangesAsync(false, token).ConfigureAwait(false);
                            context.Log(LogLevel.Warning, $"Released background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> which timed out in state <{oldState}>. State now is <{HiveLog.Job.StateParam}>", job.Id, job.Environment, job.State.Name);
                            lock (CurrentState)
                            {
                                CurrentState.ReleasedTimedOutBackgroundJobs++;
                            }
                        }
                    }
                }

                result = await GetTimedoutBackgroundJobs(context, options, token).ConfigureAwait(false);
            }
        }

        private async Task<IClientQueryResult<ILockedBackgroundJob>> GetTimedoutBackgroundJobs(IDaemonExecutionContext context, HiveMindOptions options, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            options.ValidateArgument(nameof(options));
            await using var connection = await _backgroundJobClient.OpenConnectionAsync(context.Daemon.Colony.Environment, false, token).ConfigureAwait(false);

            return await _backgroundJobClient.GetTimedOutAsync(connection, $"LockMonitor.{context.Daemon.Colony.Name}.{context.Daemon.Colony.Id}", 10, token).ConfigureAwait(false);
        }

        private class State
        {
            public long ReleasedTimedOutBackgroundJobs { get; set; }

            public override string ToString()
            {
                return $"Timed out background jobs handled: {ReleasedTimedOutBackgroundJobs}";
            }
        }
    }
}
