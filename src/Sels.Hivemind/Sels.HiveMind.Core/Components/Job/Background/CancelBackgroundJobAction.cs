using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Background
{
    /// <summary>
    /// Action that cancels a running background job.
    /// </summary>
    public class CancelBackgroundJobAction : IBackgroundJobAction
    {
        /// <inheritdoc/>
        public async Task ExecuteAsync(IBackgroundJobExecutionContext jobContext, object context, CancellationToken token)
        {
            jobContext.Log($"Background job was requested to stop. Cancelling");
            await jobContext.Job.ChangeStateAsync(new IdleState() { Reason = context.CastToOrDefault<string>() ?? "Manually cancelled" }, token).ConfigureAwait(false); // Context here is reason for cancellation
            jobContext.Cancel();
        }
    }
}
