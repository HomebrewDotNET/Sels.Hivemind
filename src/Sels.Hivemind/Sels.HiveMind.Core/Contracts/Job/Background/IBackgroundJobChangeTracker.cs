using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Tracks the changes made on a background job.
    /// </summary>
    public interface IBackgroundJobChangeTracker : IJobChangeTracker<IBackgroundJobState>
    {

    }
}
