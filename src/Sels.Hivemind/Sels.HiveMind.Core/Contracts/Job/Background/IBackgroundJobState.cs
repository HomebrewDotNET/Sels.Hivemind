using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.Background
{
    /// <summary>
    /// Represents a state that a <see cref="IReadOnlyBackgroundJob"/> can be in.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobState : IJobState
    {

    }
}
