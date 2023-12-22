using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Middleware that is to be executed in the execution chain when processing a background job.
    /// </summary>
    public interface IBackgroundJobMiddleware
    {
        /// <summary>
        /// Executes the middleware.
        /// </summary>
        /// <param name="jobContext"><inheritdoc cref="IBackgroundJobExecutionContext"/></param>
        /// <param name="context">Optional context that acts as input for the middleware</param>
        /// <param name="next">Delegate that calls the next middleware or background job in the chain. Doesn't have to be called</param>
        /// <param name="token">Token that will be cancelled when the job is requested to stop processing</param>
        /// <returns>Task containing the execution state</returns>
        public Task ExecuteAsync(IBackgroundJobExecutionContext jobContext, object context, Func<IBackgroundJobExecutionContext, CancellationToken, Task> next, CancellationToken token);
    }
}
