using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.HiveMind.Interval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.Core.Extensions.Logging;

namespace Sels.HiveMind.Calendar
{
    ///<inheritdoc cref="ICalendarProvider"/>
    public class CalendarProvider :  ComponentProvider<ICalendar>, ICalendarProvider
    {
        /// <inheritdoc cref="CalendarProvider"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public CalendarProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<ICalendar>> factories, ILogger<CalendarProvider> logger = null) : base(serviceProvider, factories, logger)
        {

        }
    }
}
