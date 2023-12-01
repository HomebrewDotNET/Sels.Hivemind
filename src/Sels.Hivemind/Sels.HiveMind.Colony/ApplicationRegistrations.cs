using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Client;
using Sels.HiveMind.Service.Job;
using Sels.HiveMind.Validation;
using Sels.HiveMind.Events;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Sels.HiveMind.Job;
using Sels.Core.ServiceBuilder;
using Sels.HiveMind.Queue;
using Sels.HiveMind.EventHandlers;
using Sels.HiveMind.RequestHandlers;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Scheduler.Lazy;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Colony.Identity;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        /// <summary>
        /// Adds all the core services needed to host HiveMind colonies.
        /// </summary>
        /// <param name="services">Collection to add the services to</param>
        /// <param name="colonyIdentityProviderRegistrationOptions"><inheritdoc cref="ColonyIdentityProviderRegistrationOptions"/></param>
        /// <returns><paramref name="services"/> for method chaining</returns>
        public static IServiceCollection AddHiveMindColony(this IServiceCollection services, ColonyIdentityProviderRegistrationOptions colonyIdentityProviderRegistrationOptions = ColonyIdentityProviderRegistrationOptions.Machine)
        {
            services.ValidateArgument(nameof(services));

            services.AddHiveMind();

            // Colony
            services.New<IColonyFactory, ColonyFactory>()
                    .AsSingleton()
                    .Trace((s, x) => x.Duration.OfAll)
                    .TryRegister();

            // Identity
            switch (colonyIdentityProviderRegistrationOptions)
            {
                case ColonyIdentityProviderRegistrationOptions.Machine:
                    services.New<IColonyIdentityProvider, MachineIdentityProvider>()
                            .AsSingleton()
                            .Trace((s, x) => x.Duration.OfAll)
                            .TryRegister();
                    break;
                case ColonyIdentityProviderRegistrationOptions.Guid:
                    services.New<IColonyIdentityProvider, MachineIdentityProvider>()
                            .AsSingleton()
                            .Trace((s, x) => x.Duration.OfAll)
                            .TryRegister();
                    break;
                case ColonyIdentityProviderRegistrationOptions.None: break;
                default: throw new NotSupportedException($"Identity provider <{colonyIdentityProviderRegistrationOptions}> is not known");
            }

            return services;
        }
    }

    /// <summary>
    /// Contains which colony identity provider to register.
    /// </summary>
    public enum ColonyIdentityProviderRegistrationOptions
    {
        /// <summary>
        /// Register no identity provider.
        /// </summary>
        None = 0,
        /// <summary>
        /// Registers <see cref="MachineIdentityProvider"/>
        /// </summary>
        Machine = 1,
        /// <summary>
        /// Registers <see cref="GuidIdentityProvider"/>
        /// </summary>
        Guid = 2
    }
}
