using Sels.Core.Mediator.Request;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Requests.Job
{
    /// <summary>
    /// Handler that can intercept states during election on a <see cref="ILockedRecurringJob"/> and choose to elect another state.
    /// </summary>
    public interface IRecurringJobStateElectionRequestHandler : IRequestHandler<RecurringJobStateElectionRequest, IRecurringJobState>
    {

    }
}
