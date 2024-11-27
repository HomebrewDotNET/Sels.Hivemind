using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Represents an action that can be scheduled to be executed on a running recurring job.                               
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobAction
    {
        /// <summary>
        /// Executes the action.
        /// </summary>
        /// <param name="jobContext"><inheritdoc cref="IRecurringJobExecutionContext"/></param>
        /// <param name="context">Optional context that acts as input for the action</param>
        /// <param name="token">Token that will be cancelled when the job is requested to stop processing</param>
        /// <returns>Task containing the execution state</returns>
        public Task ExecuteAsync(IRecurringJobExecutionContext jobContext, object context, CancellationToken token);
    }
}
