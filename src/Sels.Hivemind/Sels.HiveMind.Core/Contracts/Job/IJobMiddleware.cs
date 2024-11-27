using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Middleware that is to be executed in the execution chain when processing an instance of <typeparamref name="TJob"/>.
    /// </summary>
    /// <typeparam name="TExecutionContext">Type of the execution context that is used to execute the job</typeparam>
    /// <typeparam name="TJob">The type of job that is processed</typeparam>
    public interface IJobMiddleware<TExecutionContext, TJob> : IMiddleware
        where TExecutionContext : IJobExecutionContext<TJob>
    {
        /// <summary>
        /// Executes the middleware.
        /// </summary>
        /// <param name="jobContext"><inheritdoc cref="TExecutionContext"/></param>
        /// <param name="context">Optional context that acts as input for the middleware</param>
        /// <param name="next">Delegate that calls the next middleware or job in the chain. Doesn't have to be called</param>
        /// <param name="token">Token that will be cancelled when the job is requested to stop processing</param>
        /// <returns>Task containing the execution state</returns>
        public Task ExecuteAsync(TExecutionContext jobContext, object? context, Func<TExecutionContext, CancellationToken, Task> next, CancellationToken token);
    }
}
