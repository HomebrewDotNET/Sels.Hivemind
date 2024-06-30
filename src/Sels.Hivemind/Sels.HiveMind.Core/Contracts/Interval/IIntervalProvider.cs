using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Interval
{
    /// <summary>
    /// Provides access to <see cref="IInterval"/> of certain types.
    /// </summary>
    public interface IIntervalProvider : IComponentProvider<IInterval>
    {
    }
}
