using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Events
{
    /// <summary>
    /// Listens for events of type <see cref="SyncingColonyStateEvent"/>.
    /// </summary>
    public interface ISyncingColonyStateEventHandler : IEventListener<SyncingColonyStateEvent>
    {
    }
}
