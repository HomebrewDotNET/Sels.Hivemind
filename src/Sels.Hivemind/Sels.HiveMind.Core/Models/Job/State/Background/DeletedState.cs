using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State.Background
{
    /// <summary>
    /// State for a job that was deleted. Does not physically delete the job from the system yet.
    /// </summary>
    public class DeletedState : BaseBackgroundJobState<DeletedState>
    {

    }
}
