using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Scheduler
{
    /// <summary>
    /// Provider that is able to create <see cref="IJobScheduler"/>(s) of a certain type.
    /// </summary>
    public interface IJobSchedulerProvider : IComponentProvider<IJobScheduler, JobSchedulerConfiguration>
    {
    }
}
