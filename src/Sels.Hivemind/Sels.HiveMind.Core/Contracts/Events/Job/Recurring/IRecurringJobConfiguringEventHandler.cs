using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Contracts.Events.Job.Recurring
{
    /// <summary>
    /// Listens for events of type <see cref="RecurringJobConfiguringEvent"/>.
    /// </summary>
    public interface IRecurringJobConfiguringEventHandler : IEventListener<RecurringJobConfiguringEvent>
    {
    }
}
