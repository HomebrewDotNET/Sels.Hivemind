using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.Background
{
    /// <summary>
    /// The options used by a worker swarm that executes background jobs.
    /// </summary>
    public interface IBackgroundJobWorkerSwarmHostOptions : IWorkerSwarmHostOptions<IBackgroundJobMiddleware, IBackgroundJobWorkerSwarmHostOptions>
    {
    }
}
