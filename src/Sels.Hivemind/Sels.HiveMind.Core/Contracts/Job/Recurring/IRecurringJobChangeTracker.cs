using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Tracks the changes made on a recurring job.
    /// </summary>
    public interface IRecurringJobChangeTracker : IJobChangeTracker<IRecurringJobState>
    {

    }
}
