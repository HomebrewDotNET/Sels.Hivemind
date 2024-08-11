using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Threading;
using Sels.Core.Mediator.Request;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Requests.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.RequestHandlers.BackgroundJob
{
    /// <summary>
    /// Requeues a background job if it encountered an exception that can be retried up until a configured maximum amount.
    /// </summary>
    public class BackgroundJobRetryHandler : IBackgroundJobStateElectionRequestHandler
    {
        // Fields
        private readonly ILogger? _logger;
        private readonly IOptionsSnapshot<BackgroundJobRetryOptions> _options;

        // Properties
        /// <inheritdoc/>
        public byte? Priority => null; // Always run last to give other handlers priority.

        /// <inheritdoc cref="BackgroundJobRetryHandler"/>
        /// <param name="logger">Optional logger for tracing</param>
        /// <param name="options">Used to access the options for this instance</param>
        public BackgroundJobRetryHandler(IOptionsSnapshot<BackgroundJobRetryOptions> options, ILogger<BackgroundJobRetryHandler>? logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected BackgroundJobRetryHandler()
        {
            
        }

        /// <inheritdoc/>
        public virtual Task<RequestResponse<IBackgroundJobState>> TryRespondAsync(IRequestHandlerContext context, BackgroundJobStateElectionRequest request, CancellationToken token)
        {
            if(request.ElectedState is FailedState failedState && failedState.Exception != null)
            {
                var options = _options.Get(request.Job.Environment);

                // Get retry counts
                int currentRetryCount = request.Job.GetPropertyOrSet(HiveMindConstants.Job.Properties.RetryCount, () => 0);
                int totalRetryCount = request.Job.GetPropertyOrSet(HiveMindConstants.Job.Properties.TotalRetryCount, () => currentRetryCount);

                // Check if we can retry
                if(currentRetryCount < options.MaxRetryCount)
                {
                    _logger.Debug($"Background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}>. can be retried. Checking if exception is fatal", request.Job.Id, request.Job.Environment);

                    if (!options.IsFatal(failedState.Exception))
                    {
                        var retryDelay = currentRetryCount >= options.RetryTimes.Length ? options.RetryTimes.Last() : options.RetryTimes[currentRetryCount];
                        currentRetryCount++;
                        totalRetryCount++;
                        request.Job.SetProperty(HiveMindConstants.Job.Properties.RetryCount, currentRetryCount);
                        request.Job.SetProperty(HiveMindConstants.Job.Properties.TotalRetryCount, totalRetryCount);

                        _logger.Log($"Background job <{HiveLog.Job.IdParam}> in environment <{HiveLog.EnvironmentParam}> will be retried in <{retryDelay}> after failing due to exception <{failedState.Exception}>", request.Job.Id, request.Job.Environment);
                        return RequestResponse<IBackgroundJobState>.Success(new EnqueuedState(DateTime.Now.Add(retryDelay))
                        {
                            Reason = $"Retry {currentRetryCount} of {options.MaxRetryCount}"
                        }).ToTaskResult();
                    }
                    else
                    {
                        _logger.Warning($"Background job <{HiveLog.Job.IdParam}> ran into fatal exception <{failedState.Exception}>. Job won't be retried", request.Job.Id);
                    }
                }
            }

            return RequestResponse<IBackgroundJobState>.Reject().ToTaskResult();
        }
    }
}
