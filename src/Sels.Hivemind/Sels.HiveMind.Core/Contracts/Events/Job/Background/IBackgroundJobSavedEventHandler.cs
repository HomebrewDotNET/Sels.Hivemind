using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Background
{
    /// <summary>
    /// Listens for events of type <see cref="BackgroundJobSavedEvent"/>.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobSavedEventHandler : IEventListener<BackgroundJobSavedEvent>
    {
    }
}
