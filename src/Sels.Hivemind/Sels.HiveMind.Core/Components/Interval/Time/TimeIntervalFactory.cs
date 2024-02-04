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
    public class TimeIntervalFactory : IIntervalFactory
    {
        /// <inheritdoc />
        public string Type => TimeInterval.Type;
        
        /// <inheritdoc />
        public Task<IInterval> CreateIntervalAsync(IServiceProvider serviceProvider, CancellationToken token = default) => Task.FromResult<IInterval>(new TimeInterval());
    }
}
