using Sels.Core.Mediator.Request;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Requests.Job;
using Sels.HiveMind.Requests.Job.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    /// <summary>
    /// Request handlers that cancels the exection of a job if the tenant has not paid their bill.
    /// </summary>
    public class TenantPaymentEnforcer : IBackgroundJobStateElectionRequestHandler
    {
        /// <inheritdoc/>
        public byte? Priority => 0; // Always run first

        /// <inheritdoc/>
        public Task<RequestResponse<IBackgroundJobState>> TryRespondAsync(IRequestHandlerContext context, BackgroundJobStateElectionRequest request, CancellationToken token)
        {
            if (request.ElectedState is ExecutingState && request.Job.TryGetProperty<Guid>("TenantId", out var tenantId))
            {
                // Check if tenant has paid their bill
                if (!HasPaidBill(tenantId))
                {
                    return Task.FromResult(RequestResponse<IBackgroundJobState>.Success(new FailedState($"Tenant <{tenantId}> has not paid it's bills so not allowing execution")));
                }
            }

            return Task.FromResult(RequestResponse<IBackgroundJobState>.Reject());
        }

        private bool HasPaidBill(Guid tenantId)
        {
            // Check if tenant has paid their bill
            return false;
        }
    }
}
