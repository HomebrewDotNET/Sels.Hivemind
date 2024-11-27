using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Provides access to <see cref="ICalendar"/> with certain names.
    /// </summary>
    public interface ICalendarProvider : IComponentProvider<ICalendar>
    {
    }
}
