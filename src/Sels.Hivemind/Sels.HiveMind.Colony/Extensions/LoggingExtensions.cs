using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Extensions
{
    /// <summary>
    /// Contains extension methods that helps with logging.
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Tries to start a logging scope with <paramref name="logger"/> if it is not null.
        /// </summary>
        /// <param name="logger">The logger to start the scope with</param>
        /// <param name="context">The object to transform into logging parameters</param>
        /// <returns>The logging scope started with <paramref name="logger"/></returns>
        public static IDisposable TryBeginScope(this ILogger logger, IDaemonExecutionContext context)
        {
            context.ValidateArgument(nameof(context));

            var parameters = new Dictionary<string, object>()
            {
                { HiveLog.Colony.Name, context.Daemon.Colony.Name },
                { HiveLog.Daemon.Name, context.Daemon.Name }
            };

            return logger.TryBeginScope(parameters);
        }
    }
}
