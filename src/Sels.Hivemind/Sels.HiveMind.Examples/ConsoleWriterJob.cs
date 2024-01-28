using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    public class ConsoleWriterJob
    {
        public void Run(IBackgroundJobExecutionContext context, string message, CancellationToken token)
        {
            Console.WriteLine($"{context.Drone} says {message}");
        }

        public Task<int> RunAsync(IBackgroundJobExecutionContext context, string message, CancellationToken token)
        {
            Console.WriteLine($"{context.Drone} says {message}");
            return Task.FromResult<int>(message?.Length ?? 0);
        }
    }
}
