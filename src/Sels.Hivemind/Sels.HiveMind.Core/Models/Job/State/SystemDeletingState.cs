using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Temporary state that jobs will transition into when they are being deleted permenantly.
    /// State election can be used to avoid the job being deleted.
    /// </summary>
    public class SystemDeletingState : BaseSharedJobState<SystemDeletingState>
    {
    }
}
