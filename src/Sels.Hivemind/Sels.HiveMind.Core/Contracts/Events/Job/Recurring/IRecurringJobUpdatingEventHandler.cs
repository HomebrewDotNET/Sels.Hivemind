﻿using Sels.Core.Mediator.Event;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Listens for events of type <see cref="RecurringJobUpdatingEvent"/>.
    /// </summary>
    public interface IRecurringJobUpdatingEventHandler : IEventListener<RecurringJobUpdatingEvent>
    {
    }
}
