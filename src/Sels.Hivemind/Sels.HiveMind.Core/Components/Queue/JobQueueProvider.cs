using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Queue
{
    /// <inheritdoc cref="IJobQueueProvider"/>
    public class JobQueueProvider : ComponentProvider<IJobQueue>, IJobQueueProvider
    {
        /// <inheritdoc cref="JobQueueProvider"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public JobQueueProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<IJobQueue>> factories, ILogger<JobQueueProvider>? logger = null) : base(serviceProvider, factories, logger)
        {

        }
    }
}
