using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Events
{
    /// <summary>
    /// Listens for events of type <see cref="ColonyCreatedEvent"/>.
    /// </summary>
    public interface IColonyCreatedEventHandler : IEventListener<ColonyCreatedEvent>
    {

    }
}
