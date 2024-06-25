using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a state that a <see cref="IReadOnlyRecurringJob"/> can be in.
    /// </summary>
    public interface IRecurringJobState : IJobState
    {
    }
}
