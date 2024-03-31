using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Listens for events of type <see cref="BackgroundJobSavingEvent"/>.
    /// </summary>
    public interface IBackgroundJobSavingEventHandler : IEventListener<BackgroundJobSavingEvent>
    {
    }
}
