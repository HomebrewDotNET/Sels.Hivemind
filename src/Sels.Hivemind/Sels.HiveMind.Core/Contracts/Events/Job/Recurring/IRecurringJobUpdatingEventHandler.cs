using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Recurring
{
    /// <summary>
    /// Listens for events of type <see cref="RecurringJobUpdatingEvent"/>.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobUpdatingEventHandler : IEventListener<RecurringJobUpdatingEvent>
    {
    }
}
