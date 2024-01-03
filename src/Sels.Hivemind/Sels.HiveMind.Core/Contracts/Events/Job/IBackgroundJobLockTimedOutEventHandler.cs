using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Listens for events of type <see cref="BackgroundJobLockTimedOutEvent"/>.
    /// </summary>
    public interface IBackgroundJobLockTimedOutEventHandler : IEventListener<BackgroundJobLockTimedOutEvent>
    {

    }
}
