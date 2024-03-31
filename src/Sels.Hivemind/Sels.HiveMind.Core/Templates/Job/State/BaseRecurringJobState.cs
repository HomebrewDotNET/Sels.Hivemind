using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Base class for creating a <see cref="IRecurringJobState"/>.
    /// </summary>
    /// <typeparam name="T">The type inheriting from the current class</typeparam>
    public abstract class BaseRecurringJobState<T> : BaseJobState<T>, IRecurringJobState
    {
        /// <inheritdoc/>
        long IRecurringJobState.Sequence { get; set; }
    }
}
