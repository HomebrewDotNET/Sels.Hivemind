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
    public class CalendarProvider : ICalendarProvider
    {
        // Fields
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<ICalendarFactory> _factories;

        /// <inheritdoc cref="CalendarProvider"/>
        /// <param name="serviceProvider">Provider to use to resolve any dependencies for created calendars</param>
        /// <param name="factories">Any registered interval factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public CalendarProvider(IServiceProvider serviceProvider, IEnumerable<ICalendarFactory> factories, ILogger<CalendarProvider> logger = null)
        {
            _serviceProvider = serviceProvider.ValidateArgument(nameof(serviceProvider));
            _factories = factories.ValidateArgument(nameof(factories));
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IComponent<ICalendar>> GetCalendarAsync(string name, CancellationToken token = default)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            _logger.Log($"Creating new calendar with name <{name}>");

            var factory = _factories.LastOrDefault(x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create calendars with name <{name}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var calendar = await factory.CreateCalendarAsync(scope.ServiceProvider, token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Calendar factory with name <{factory.Name}> returned null");
                _logger.Log($"Created <{calendar}> with name <{name}>");
                return new ScopedComponent<ICalendar>(calendar, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
