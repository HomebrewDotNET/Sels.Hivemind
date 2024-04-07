using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Models.Job.State
{
    /// <summary>
    /// Final state that will be used when a job was permanently deleted from the system.
    /// </summary>
    public class SystemDeletedState : BaseSharedJobState<SystemDeletedState>
    {

    }
}
