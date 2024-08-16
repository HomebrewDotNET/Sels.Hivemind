using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Background
{
    /// <summary>
    /// Middleware that is to be executed in the execution chain when processing a background job.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobMiddleware : IJobMiddleware<IBackgroundJobExecutionContext, IWriteableBackgroundJob>
    {

    }
}
