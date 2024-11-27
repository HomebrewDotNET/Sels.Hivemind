﻿using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Recurring
{
    /// <summary>
    /// Listens for events of type <see cref="RecurringJobPersistedEvent"/>.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobPersistedEventHandler : IEventListener<RecurringJobPersistedEvent>
    {
    }
}
