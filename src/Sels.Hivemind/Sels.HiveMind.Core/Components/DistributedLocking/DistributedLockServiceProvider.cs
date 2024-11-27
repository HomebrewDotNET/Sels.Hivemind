using Sels.HiveMind.Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.DistributedLocking
{
    /// <inheritdoc cref="IDistributedLockServiceProvider"/>
    public class DistributedLockServiceProvider : ComponentProvider<IDistributedLockService>, IDistributedLockServiceProvider
    {
        /// <inheritdoc cref="DistributedLockServiceProvider"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public DistributedLockServiceProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<IDistributedLockService>> factories, ILogger<DistributedLockServiceProvider> logger = null) : base(serviceProvider, factories, logger)
        {

        }
    }
}
