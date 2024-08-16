using Sels.Core.Mediator.Request;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Requests.Job.Background
{
    /// <summary>
    /// Handler that can intercept states during election on a <see cref="ILockedBackgroundJob"/> and choose to elect another state.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobStateElectionRequestHandler : IRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState?>
    {

    }
}
