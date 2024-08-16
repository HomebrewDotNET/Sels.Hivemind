using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Job.Recurring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.Recurring
{
    /// <summary>
    /// The options used by a worker swarm that executes recurring jobs.
    /// </summary>
    public interface IRecurringJobWorkerSwarmHostOptions : IWorkerSwarmHostOptions<IRecurringJobMiddleware, IRecurringJobWorkerSwarmHostOptions>
    {
    }
}
