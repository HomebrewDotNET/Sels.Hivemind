using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Base class for creating a <see cref="IBackgroundJobState"/>.
    /// </summary>
    /// <typeparam name="T">The type inheriting from the current class</typeparam>
    public abstract class BaseBackgroundJobState<T> : BaseJobState<T>, IBackgroundJobState
    {
    }
}
