using Sels.Core.Mediator.Request;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Requests.Job.Recurring;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Requests.Job.Recurring
{
    /// <summary>
    /// Handler that can intercept states during election on a <see cref="ILockedRecurringJob"/> and choose to elect another state.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobStateElectionRequestHandler : IRequestHandler<RecurringJobStateElectionRequest, IRecurringJobState?>
    {

    }
}
