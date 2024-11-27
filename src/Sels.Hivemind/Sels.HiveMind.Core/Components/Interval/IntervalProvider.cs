using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sels.HiveMind.Interval
{
    /// <inheritdoc cref="IIntervalProvider"/>
    public class IntervalProvider : ComponentProvider<IInterval>, IIntervalProvider
    {
        /// <inheritdoc cref="IntervalProvider"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public IntervalProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<IInterval>> factories, ILogger<IntervalProvider> logger = null) : base(serviceProvider, factories, logger)
        {

        }
    }
}
