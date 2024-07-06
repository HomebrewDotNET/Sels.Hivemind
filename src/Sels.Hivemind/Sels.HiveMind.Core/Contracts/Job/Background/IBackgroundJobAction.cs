using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents an action that can be scheduled to be executed on a running background job.                               
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobAction
    {
        /// <summary>
        /// Executes the action.
        /// </summary>
        /// <param name="jobContext"><inheritdoc cref="IBackgroundJobExecutionContext"/></param>
        /// <param name="context">Optional context that acts as input for the action</param>
        /// <param name="token">Token that will be cancelled when the job is requested to stop processing</param>
        /// <returns>Task containing the execution state</returns>
        public Task ExecuteAsync(IBackgroundJobExecutionContext jobContext, object context, CancellationToken token);
    }
}
