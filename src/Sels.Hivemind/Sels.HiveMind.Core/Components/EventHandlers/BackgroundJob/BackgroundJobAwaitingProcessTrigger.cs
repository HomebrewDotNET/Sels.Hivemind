using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Mediator.Event;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Client;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Requests.Job;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers.BackgroundJob
{
    /// <summary>
    /// Event handler that enqueues awaiting background jobs if a job enters a final state.
    /// </summary>
    public class BackgroundJobAwaitingProcessTrigger : IBackgroundJobFinalStateElectedEventHandler, IBackgroundJobStateElectionRequestHandler
    {
        // Fields
        private readonly IBackgroundJobClient _client;
        private readonly ILogger _logger;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => 100;

        /// <inheritdoc cref="BackgroundJobAwaitingProcessTrigger"/>
        /// <param name="client">Client used to query awaiting jobs</param>
        /// <param name="service">Used to acquire distributed locks on background jobs</param>
        /// <param name="logger">Optional logger for tracing</param>
        public BackgroundJobAwaitingProcessTrigger(IBackgroundJobClient client, ILogger<BackgroundJobAwaitingProcessTrigger> logger = null)
        {
            _client = client.ValidateArgument(nameof(client));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected BackgroundJobAwaitingProcessTrigger()
        {

        }

        /// <inheritdoc/>
        public virtual async Task HandleAsync(IEventListenerContext context, BackgroundJobFinalStateElectedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));
            var job = @event.Job;
            var electedState = @event.FinalState;
            var connection = @event.Connection;

            //// Set continuation
            if (job.State is AwaitingState awaitingState)
            {
                await SetContinuationAsync(connection, job, awaitingState, token).ConfigureAwait(false);
            }

            //// Trigger continuations
            // Don't trigger for new jobs
            if (job.ChangeTracker.NewStates.OfType<CreatedState>().Any()) return;

            _logger.Log($"Checking if there are background jobs awaiting background job {HiveLog.Job.Id} in environment <{HiveLog.Environment}> that transitioned into {HiveLog.Job.State}", job.Id, job.Environment, electedState.Name);

            // Use distributed lock to handle race conditions
            await using (await job.AcquireStateLock(connection, token).ConfigureAwait(false))
            {
                var continuations = await job.GetDataOrDefaultAsync<Continuation[]>(connection, HiveMindConstants.Job.Data.ContinuationsName, token).ConfigureAwait(false);

                if (continuations.HasValue())
                {
                    bool anyTriggered = false;

                    foreach (var continuation in continuations.Where(x => !x.WasTriggered))
                    {
                        _logger.Debug($"Checking if awaiting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment} can be enqueued", continuation.AwaitingJobId, job.Environment);

                        var newState = GetAwaitingJobNewState(job, continuation, false);

                        if (newState != null)
                        {
                            _logger.Debug($"Fetching awaiting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment} with write lock to set state <{newState}>", continuation.AwaitingJobId, job.Environment);

                            var awaitingJob = await _client.GetWithLockAsync(connection, continuation.AwaitingJobId, $"AwaitingHandler.{job.Id}", token).ConfigureAwait(false);
                            connection.OnDispose(awaitingJob.DisposeAsync); // Dispose job when connection is disposed

                            await awaitingJob.ChangeStateAsync(connection, newState, token).ConfigureAwait(false);
                            await awaitingJob.SaveChangesAsync(connection, false, token).ConfigureAwait(false);

                            _logger.Log($"Handled awaiting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}. Job state is now <{HiveLog.Job.State}>", continuation.AwaitingJobId, job.Environment, newState.Name);
                            continuation.WasTriggered = true;
                            anyTriggered = true;
                        }
                        else
                        {
                            _logger.Debug($"Awaiting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment} can not be enqueued yet", continuation.AwaitingJobId, job.Environment);
                        }
                    }

                    if (anyTriggered)
                    {
                        _logger.Debug($"Persisting continuation state for background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}", job.Id, job.Environment);
                        await job.SetDataAsync(connection, HiveMindConstants.Job.Data.ContinuationsName, continuations, token);
                    }

                    return;
                }

                _logger.Log($"No continuations for background job {HiveLog.Job.Id} in environment <{HiveLog.Environment} that transitioned into {HiveLog.Job.State}", job.Id, job.Environment, electedState.Name);
            }
        }
        private async Task SetContinuationAsync(IStorageConnection storageConnection, IReadOnlyBackgroundJob awaitingJob, AwaitingState awaitingState, CancellationToken token)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            awaitingJob.ValidateArgument(nameof(awaitingJob));
            awaitingState.ValidateArgument(nameof(awaitingState));

            // Use distributed lock to handle race conditions
            _logger.Debug($"Acquiring distributed lock on parent background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to set continuation", awaitingState.JobId, storageConnection.Environment);
            await using (await storageConnection.Storage.AcquireDistributedLockForBackgroundJobAsync(storageConnection, awaitingState.JobId, token).ConfigureAwait(false))
            {
                _logger.Debug($"Fetching parent background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to set continuation", awaitingState.JobId, storageConnection.Environment);
                await using (var parentJob = await _client.GetAsync(storageConnection, awaitingState.JobId, token).ConfigureAwait(false))
                {
                    var continuations = await parentJob.GetDataOrDefaultAsync<List<Continuation>>(storageConnection, HiveMindConstants.Job.Data.ContinuationsName, token).ConfigureAwait(false);
                    var continuation = new Continuation()
                    {
                        AwaitingJobId = awaitingJob.Id,
                        ValidStates = awaitingState.ValidStates,
                        DelayBy = awaitingState.DelayBy
                    };

                    continuations ??= new List<Continuation>();
                    continuations.Add(continuation);

                    // Persist
                    await parentJob.SetDataAsync(storageConnection, HiveMindConstants.Job.Data.ContinuationsName, continuations, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc/>
        public virtual async Task<RequestResponse<IBackgroundJobState>> TryRespondAsync(IRequestHandlerContext context, BackgroundJobStateElectionRequest request, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            request.ValidateArgument(nameof(request));

            var job = request.Job;
            var connection = request.StorageConnection;

            if (request.ElectedState is AwaitingState awaitingState)
            {
                _logger.Log($"Checking if background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> that is awaiting job <{awaitingState.JobId}> can already be enqueued", job.Id, job.Environment);

                IBackgroundJobState newState;
                if (connection != null)
                {
                    newState = await CanTriggerAsync(connection, job, awaitingState, token).ConfigureAwait(false);
                }
                else
                {
                    await using (var clientConnection = await _client.OpenConnectionAsync(job.Environment, false, token).ConfigureAwait(false))
                    {
                        newState = await CanTriggerAsync(clientConnection.StorageConnection, job, awaitingState, token).ConfigureAwait(false);
                    }
                }

                if (newState != null)
                {
                    _logger.Log($"Awaiting background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> can already transition into state <{newState}>", job.Id, job.Environment);
                    return RequestResponse<IBackgroundJobState>.Success(newState);
                }
            }

            return RequestResponse<IBackgroundJobState>.Reject();
        }

        private async Task<IBackgroundJobState> CanTriggerAsync(IStorageConnection storageConnection, IReadOnlyBackgroundJob awaitingJob, AwaitingState awaitingState, CancellationToken token)
        {
            storageConnection.ValidateArgument(nameof(storageConnection));
            awaitingJob.ValidateArgument(nameof(awaitingJob));
            awaitingState.ValidateArgument(nameof(awaitingState));

            _logger.Debug($"Fetching parent background job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> to see if awaiting job can already be enqueued", awaitingState.JobId, storageConnection.Environment);
            await using (var parentJob = await _client.GetAsync(storageConnection, awaitingState.JobId, token).ConfigureAwait(false))
            {
                var continuation = new Continuation()
                {
                    AwaitingJobId = awaitingJob.Id,
                    ValidStates = awaitingState.ValidStates,
                    DelayBy = awaitingState.DelayBy
                };

                // Check if we can already trigger awaiting job
                return GetAwaitingJobNewState(parentJob, continuation, true);
            }
        }

        private IBackgroundJobState GetAwaitingJobNewState(IReadOnlyBackgroundJob job, Continuation continuation, bool duringElection)
        {
            job.ValidateArgument(nameof(job));
            continuation.ValidateArgument(nameof(continuation));

            // Job can be enqueued
            if (continuation.ValidStates == null || continuation.ValidStates.Contains(job.State.Name, StringComparer.OrdinalIgnoreCase))
            {
                _logger.Log($"Background job {continuation.AwaitingJobId} awaiting background job {HiveLog.Job.Id} which was elected to state {HiveLog.Job.State} can be enqueued because it was awaiting states <{(continuation.ValidStates.HasValue() ? continuation.ValidStates.JoinString(", ") : "Any")}>", job.Id, job.State.Name);

                return new EnqueuedState()
                {
                    DelayedToUtc = continuation.DelayBy.HasValue ? DateTime.UtcNow.Add(continuation.DelayBy.Value) : (DateTime?)null,
                    Reason = $"Parent background job <{job.Id}> transitioned into state <{job.State.Name}>"
                };
            }
            // Job needs to be deleted
            else if (!duringElection && continuation.DeleteOnOtherState)
            {
                _logger.Log($"Background job {continuation.AwaitingJobId} awaiting background job {HiveLog.Job.Id} which was elected to state {HiveLog.Job.State} is not in not valid states <{continuation.ValidStates.JoinString(", ")}> so will be deleted because {nameof(continuation.DeleteOnOtherState)} was set to true", job.Id, job.State.Name);
                return new DeletedState()
                {
                    Reason = $"Parent background job <{job.Id}> transitioned into state <{job.State.Name}> which is not in valid states <{continuation.ValidStates.JoinString(", ")}> and {nameof(continuation.DeleteOnOtherState)} was set to true"
                };
            }

            return null;
        }
    }
}
