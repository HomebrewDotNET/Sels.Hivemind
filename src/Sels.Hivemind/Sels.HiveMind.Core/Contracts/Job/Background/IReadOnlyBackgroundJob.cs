using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Background
{
    /// <summary>
    /// Represents a read-only background job with it's current state.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IReadOnlyBackgroundJob : IReadOnlyJob<ILockedBackgroundJob, IBackgroundJobChangeTracker, IBackgroundJobState, IBackgroundJobAction>
    {
    }
}
