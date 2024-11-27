using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Queue
{
    /// <summary>
    /// Provides access to <see cref="IJobQueue"/> configured for a certain HiveMind environment.
    /// </summary>
    public interface IJobQueueProvider : IComponentProvider<IJobQueue>
    {
    }
}
