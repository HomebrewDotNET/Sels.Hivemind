using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Client;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Requests.Job;
using Sels.HiveMind.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers
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
        public ushort? Priority => 100;

        /// <inheritdoc cref="BackgroundJobAwaitingProcessTrigger"/>
        /// <param name="client">Client used to query awaiting jobs</param>
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
            List<IClientQueryResult<ILockedBackgroundJob>> results = new List<IClientQueryResult<ILockedBackgroundJob>>();
            var job = @event.Job;
            var electedState = @event.FinalState;
            var connection = @event.Connection;

            // Don't trigger for new jobs
            if (job.ChangeTracker.NewStates.OfType<CreatedState>().Any()) return;

            _logger.Log($"Checking if there are background job awaiting background job {HiveLog.Job.Id} that transitioned into {HiveLog.BackgroundJob.State}", job.Id, electedState.Name);

            try
            {
                IClientQueryResult<ILockedBackgroundJob> result;
                do
                {
                    // Only return jobs awaiting current job and that are awaiting any state or the elected state of the job or if they need to be deleted on an invalid state
                    result = await _client.DequeueAsync(connection, x =>
                    {
                        return x.CurrentState.Name.EqualTo(AwaitingState.StateName)
                                .And.CurrentState.Property<AwaitingState>(x => x.JobId).EqualTo(job.Id)
                                .And.Group(x => x.CurrentState.Property<AwaitingState>(x => x.ValidStateNames).EqualTo(null)
                                                 .Or.CurrentState.Property<AwaitingState>(x => x.ValidStateNames).Like($"*{electedState.Name}*")
                                                 .Or.CurrentState.Property<AwaitingState>(x => x.DeleteOnOtherState).EqualTo(true)
                                );
                    }, requester: $"AwaitingHandler{job.Id}", allowAlreadyLocked: true, token: token).ConfigureAwait(false);

                    results.Add(result);

                    if (result.Results.HasValue())
                    {
                        _logger.Debug($"Got <{result.Results.Count}> jobs awaiting {HiveLog.Job.Id} that transitioned into state {HiveLog.BackgroundJob.State}", job.Id, electedState.Name);

                        foreach (var awaitingJob in result.Results)
                        {
                            var newState = HandleJob(job, awaitingJob, false);
                            if(newState == null)
                            {
                                throw new InvalidOperationException($"Background job <{awaitingJob.Id}> awaiting background job <{job.Id}> contains invalid state that can't be handled");
                            }

                            // Save changes using the current connection
                            await awaitingJob.ChangeStateAsync(newState, token).ConfigureAwait(false);
                            await awaitingJob.SaveChangesAsync(connection, false, token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        _logger.Debug($"Got no jobs awaiting {HiveLog.Job.Id} that transitioned into state {HiveLog.BackgroundJob.State}", job.Id, electedState.Name);
                    }
                }
                // Keep looping until we have all awaiting jobs
                while (result.Results.Count != result.Total);
            }
            // Release results
            finally
            {
                // If a transaction is running we release the results after the commit
                if(connection.HasTransaction) connection.StorageConnection.OnCommitted(t => ReleaseResults(results));
                // No transaction so just release
                else await ReleaseResults(results).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public virtual async Task<RequestResponse<IBackgroundJobState>> TryRespondAsync(IRequestHandlerContext context, BackgroundJobStateElectionRequest request, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            request.ValidateArgument(nameof(request));

            var job = request.Job;
          
            if (job.State is AwaitingState awaitingState)
            {
                _logger.Log($"Checking if parent job that job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is awaiting is in a valid state so awaiting job can be enqueued", job.Id, job.Environment);

                await using (var connection = await _client.OpenConnectionAsync(job.Environment, false, token).ConfigureAwait(false))
                {
                    var parentJob = await _client.TryGetAsync(connection, awaitingState.JobId, token).ConfigureAwait(false);

                    if(parentJob != null)
                    {
                        await using (parentJob)
                        {
                            var newState = HandleJob(parentJob, job, true);
                            if (newState != null)
                            {
                                return RequestResponse<IBackgroundJobState>.Success(newState);
                            }
                        }
                    }
                    else
                    {
                        _logger.Log($"Parent job that job <{HiveLog.Job.Id}> in environment <{HiveLog.Environment}> is awaiting does not exists. Job might still be commiting", job.Id);
                    }
                }
            }

            return RequestResponse<IBackgroundJobState>.Reject();
        }
        private IBackgroundJobState HandleJob(IReadOnlyBackgroundJob job, IWriteableBackgroundJob awaitingJob, bool isDuringElection)
        {
            job.ValidateArgument(nameof(job));
            awaitingJob.ValidateArgument(nameof(awaitingJob));

            var awaitingState = awaitingJob.State.CastToOrDefault<AwaitingState>() ?? throw new InvalidOperationException($"Expected awaiting job state to be of type <{typeof(AwaitingState)}> but got state <{awaitingJob.State}>");

            // Job can be enqueued
            if (awaitingState.ValidStates == null || awaitingState.ValidStates.Contains(job.State.Name, StringComparer.OrdinalIgnoreCase))
            {
                _logger.Log($"Background job {HiveLog.Job.Id} awaiting background job {HiveLog.Job.Id} which was elected to state {HiveLog.BackgroundJob.State} can be enqueued because it was awaiting states <{(awaitingState.ValidStates.HasValue() ? awaitingState.ValidStateNames : "Any")}>", awaitingJob.Id, job.Id, job.State.Name);

                return new EnqueuedState()
                {
                    DelayedToUtc = awaitingState.DelayBy.HasValue ? DateTime.UtcNow.Add(awaitingState.DelayBy.Value) : (DateTime?)null,
                    Reason = $"Parent background job <{job.Id}> transitioned into state <{job.State.Name}>"
                };
            }
            // Job needs to be deleted
            else if (!isDuringElection && awaitingState.DeleteOnOtherState)
            {
                _logger.Log($"Background job {HiveLog.Job.Id} awaiting background job {HiveLog.Job.Id} which was elected to state {HiveLog.BackgroundJob.State} is not in not valid states <{awaitingState.ValidStateNames}> so will be deleted because {nameof(awaitingState.DeleteOnOtherState)} was set to true", awaitingJob.Id, job.Id, job.State.Name);
                return new DeletedState()
                {
                    Reason = $"Parent background job <{job.Id}> transitioned into state <{job.State.Name}> which is not in valid states <{awaitingState.ValidStateNames}> and {nameof(awaitingState.DeleteOnOtherState)} was set to true"
                };
            }

            return null;
        }

        private async Task ReleaseResults(IEnumerable<IClientQueryResult<ILockedBackgroundJob>> results)
        {
            var exceptions = new List<Exception>();

            if (results.HasValue())
            {
                foreach(var result in results)
                {
                    try
                    {
                        await result.DisposeAsync().ConfigureAwait(false);
                    }
                    catch(Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            }

            if (exceptions.HasValue()) throw new AggregateException(exceptions);
        }

    }
}
