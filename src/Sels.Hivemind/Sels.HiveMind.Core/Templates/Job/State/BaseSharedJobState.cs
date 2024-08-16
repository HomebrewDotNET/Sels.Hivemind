using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.State.Recurring;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Base class for creating a job state that is used by both recurring and background jobs.
    /// </summary>
    /// <typeparam name="T">The type inheriting from the current class</typeparam>
    public abstract class BaseSharedJobState<T> : BaseRecurringJobState<T>, IBackgroundJobState
    {
    }
}
