using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Scheduler
{
    /// <inheritdoc cref="IJobSchedulerProvider"/>
    public class JobSchedulerProvider : ComponentProvider<IJobScheduler, JobSchedulerConfiguration>, IJobSchedulerProvider
    {
        /// <inheritdoc cref="JobSchedulerProvider"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public JobSchedulerProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<IJobScheduler, JobSchedulerConfiguration>> factories, ILogger<JobSchedulerProvider>? logger = null) : base(serviceProvider, factories, logger)
        {

        }
    }
}
