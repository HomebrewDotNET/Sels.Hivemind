using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Background
{
    /// <summary>
    /// Tracks the changes made on a background job.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobChangeTracker : IJobChangeTracker<IBackgroundJobState>
    {

    }
}
