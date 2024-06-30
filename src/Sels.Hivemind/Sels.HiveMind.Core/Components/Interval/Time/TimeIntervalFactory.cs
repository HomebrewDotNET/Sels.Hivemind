using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Interval
{
    /// <summary>
    /// Factory for creating <see cref="TimeInterval"/> instances.
    /// </summary>
    public class TimeIntervalFactory : IComponentFactory<IInterval> 
    {
        /// <inheritdoc />
        public string Name => TimeInterval.Type;
        
        /// <inheritdoc />
        public Task<IInterval> CreateAsync(IServiceProvider serviceProvider, CancellationToken token = default) => Task.FromResult<IInterval>(new TimeInterval());
    }
}
