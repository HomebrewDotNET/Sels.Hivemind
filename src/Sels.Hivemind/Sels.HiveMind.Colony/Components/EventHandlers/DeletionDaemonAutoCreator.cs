using Microsoft.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Colony.EventHandlers;
using Sels.HiveMind.Colony.Events;
using Sels.HiveMind.Colony.SystemDaemon;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using System.Linq;
using Sels.Core.Extensions.Reflection;

namespace Sels.HiveMind.Colony.EventHandlers
{
    /// <summary>
    /// Creates an auto managed <see cref="DeletionDaemonAutoCreator"/> if the option is enabled on newly created colonies.
    /// </summary>
    public class DeletionDaemonAutoCreator : IColonyCreatedEventHandler
    {
        // Fields
        private readonly ILogger _logger;

        /// <inheritdoc cref="DeletionDaemonAutoCreator"/>
        /// <param name="logger">Optional logger for tracing</param>
        public DeletionDaemonAutoCreator(ILogger<DeletionDaemonAutoCreator> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected DeletionDaemonAutoCreator() { }

        /// <inheritdoc/>
        public byte? Priority => null;

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, ColonyCreatedEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var colony = @event.Colony;

            if (colony.Options.CreationOptions.HasFlag(HiveColonyCreationOptions.AutoCreateDeletionDaemon))
            {
                _logger.Log($"Auto creating deletion daemon for colony <{HiveLog.Colony.NameParam}>", colony.Name);
                var existing = colony.Daemons.FirstOrDefault(x => x.InstanceType != null && x.InstanceType.Is<DeletionDaemon>());

                if (existing != null)
                {
                    _logger.Warning($"Could not auto create deletion daemon because daemon <{HiveLog.Daemon.NameParam}> already exists which is the same type", existing.Name);
                    return Task.CompletedTask;
                }

                colony.WithDeletionDaemon(daemonName: "$DeletionDaemon", daemonBuilder: x => x.WithRestartPolicy(DaemonRestartPolicy.Always)
                                                                                              .WithProperty(HiveMindColonyConstants.Daemon.IsAutoCreatedProperty, true));
            }

            return Task.CompletedTask;
        }
    }
}
