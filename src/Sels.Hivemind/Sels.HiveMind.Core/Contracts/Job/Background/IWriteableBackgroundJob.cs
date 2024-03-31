using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a background job that can be modified.
    /// </summary>
    public interface IWriteableBackgroundJob :  IWriteableJob<ILockedBackgroundJob, IBackgroundJobChangeTracker, IBackgroundJobState, IBackgroundJobAction>, IReadOnlyBackgroundJob
    {

    }
}
