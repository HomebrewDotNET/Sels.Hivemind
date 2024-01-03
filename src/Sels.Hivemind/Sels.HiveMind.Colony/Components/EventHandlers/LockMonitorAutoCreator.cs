using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Colony.Events;
using Sels.HiveMind.Colony.SystemDaemon;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.EventHandlers
{
    /// <summary>
    /// Creates a <see cref="LockMonitorDaemon"/> if the option is enabled.
    /// </summary>
    public class LockMonitorAutoCreator : IColonyCreatedEventHandler
    {
        // Fields
        private readonly ILogger _logger;

        /// <inheritdoc cref="LockMonitorAutoCreator"/>
        /// <param name="logger">Optional logger for tracing</param>
        public LockMonitorAutoCreator(ILogger<LockMonitorAutoCreator> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected LockMonitorAutoCreator() { }

        /// <inheritdoc/>
        public ushort? Priority => null;

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, ColonyCreatedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var colony = @event.Colony;

            if (colony.Options.CreationOptions.HasFlag(HiveColonyCreationOptions.AutoCreateLockMonitor))
            {
                _logger.Log($"Auto creating lock monitor daemon for colony <{HiveLog.Colony.Name}>", colony.Name);

                colony.WithDaemonExecutor<LockMonitorDaemon>("$LockMonitor", null, null, x => x.WithRestartPolicy(DaemonRestartPolicy.Always)
                                                                                               .WithProperty(HiveMindColonyConstants.Daemon.IsAutoCreatedProperty, true));
            }

            return Task.CompletedTask;
        }
    }
}
