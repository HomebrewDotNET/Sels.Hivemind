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
        private readonly ILogger? _logger;

        /// <inheritdoc cref="DeletionDaemonAutoCreator"/>
        /// <param name="logger">Optional logger for tracing</param>
        public DeletionDaemonAutoCreator(ILogger<DeletionDaemonAutoCreator>? logger = null)
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

            if (colony.Options.CreationOptions.HasFlag(ColonyCreationOptions.AutoCreateDeletionDaemon))
            {
                const string daemonName = "$DeletionDaemon";

                _logger.Log($"Auto creating deletion daemon <{HiveLog.Daemon.NameParam}> for colony <{HiveLog.Colony.NameParam}>", daemonName, colony.Name);
                var existing = colony.Daemons.FirstOrDefault(x => x.InstanceType != null && (x.InstanceType.Is<SystemDeletingDeletionDaemon>() || x.InstanceType.Is<BulkDeletingDeletionDaemon>()));

                if (existing != null)
                {
                    _logger.Warning($"Could not auto create deletion daemon because daemon <{HiveLog.Daemon.NameParam}> already exists which is the same type", existing.Name);
                    return Task.CompletedTask;
                }

                switch (colony.Options.DeletionMode)
                {
                    case DeletionMode.Bulk:
                        colony.WithBulkDeletionDaemon(daemonName: daemonName, daemonBuilder: x => x.WithRestartPolicy(DaemonRestartPolicy.Always)
                                                                                                          .WithProperty(HiveMindConstants.Daemon.IsAutoCreatedProperty, true));
                        break;
                    case DeletionMode.System:
                        colony.WithSystemDeletingDeletionDaemon(daemonName: daemonName, daemonBuilder: x => x.WithRestartPolicy(DaemonRestartPolicy.Always)
                                                                                                                    .WithProperty(HiveMindConstants.Daemon.IsAutoCreatedProperty, true));
                        break;
                    default: throw new NotSupportedException($"Deletion mode {colony.Options.DeletionMode} is not supported");
                }

                
            }

            return Task.CompletedTask;
        }
    }
}
