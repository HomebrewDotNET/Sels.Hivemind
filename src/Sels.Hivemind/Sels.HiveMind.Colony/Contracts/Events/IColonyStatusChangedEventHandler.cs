using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Events
{
    /// <summary>
    /// Listens for events of type <see cref="ColonyStatusChangedEvent"/>.
    /// </summary>
    public interface IColonyStatusChangedEventHandler : IEventListener<ColonyStatusChangedEvent>
    {
    }
}
