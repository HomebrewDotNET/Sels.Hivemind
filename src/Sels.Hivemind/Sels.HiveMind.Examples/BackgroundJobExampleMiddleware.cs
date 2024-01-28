using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    /// <summary>
    /// Example middleware.
    /// </summary>
    public class BackgroundJobExampleMiddleware : IBackgroundJobMiddleware
    {
        /// <inheritdoc/>
        public async Task ExecuteAsync(IBackgroundJobExecutionContext jobContext, object context, Func<IBackgroundJobExecutionContext, CancellationToken, Task> next, CancellationToken token)
        {
            var arguments = jobContext.InvocationArguments; // Get arguments for the job method
            var job = jobContext.Job; // Access writeable job

            string input = context as string; // Custom input for middleware

            // Do stuff before the job is executed

            await next(jobContext, token); // Call next middleare or invoke the job

            // Do stuff after the job is executed

            var result = jobContext.Result; // Get the result of the job. Either object returned by method or exception
        }
    }
}
