using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Client;
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
using Sels.HiveMind.Colony;
using Sels.HiveMind.Colony.Identity;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sels.HiveMind.Colony.EventHandlers;
using Sels.HiveMind.Colony.Events;
using Sels.ObjectValidationFramework.Extensions.Validation;
using Sels.HiveMind.Colony.Options;
using Sels.HiveMind.Colony.Swarm.Job.Background;
using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Colony.Swarm.Job.Recurring;

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

            // Activator
            services.TryAddSingleton<IActivator, ServiceProviderActivator>();

            // Colony
            services.New<IColonyFactory, ColonyFactory>()
                    .AsSingleton()
                    .Trace((s, x) => x.Duration.OfAll.And.WithScope.ForAll)
                    .TryRegister();

            // Identity
            switch (colonyIdentityProviderRegistrationOptions)
            {
                case ColonyIdentityProviderRegistrationOptions.Machine:
                    services.New<IColonyIdentityProvider, MachineIdentityProvider>()
                            .AsSingleton()
                            .Trace((s, x) => x.Duration.OfAll.And.WithScope.ForAll)
                            .TryRegister();
                    break;
                case ColonyIdentityProviderRegistrationOptions.Guid:
                    services.New<IColonyIdentityProvider, GuidIdentityProvider>()
                            .AsSingleton()
                            .Trace((s, x) => x.Duration.OfAll.And.WithScope.ForAll)
                            .TryRegister();
                    break;
                case ColonyIdentityProviderRegistrationOptions.None: break;
                default: throw new NotSupportedException($"Identity provider <{colonyIdentityProviderRegistrationOptions}> is not known");
            }

            // Options
            services.AddOptions();
            services.BindOptionsFromConfig<BackgroundJobWorkerSwarmDefaultHostOptions>();
            services.AddValidationProfile<WorkerSwarmDefaultHostOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<BackgroundJobWorkerSwarmDefaultHostOptions, WorkerSwarmDefaultHostOptionsValidationProfile>();
            services.BindOptionsFromConfig<RecurringJobWorkerSwarmDefaultHostOptions>();
            services.AddOptionProfileValidator<RecurringJobWorkerSwarmDefaultHostOptions, WorkerSwarmDefaultHostOptionsValidationProfile>();

            services.AddValidationProfile<DeletionDeamonOptionsValidationProfile, string>();
            services.BindOptionsFromConfig<AutoCreateRecurringJobWorkerSwarmHostOptions>();
            services.AddSingleton<AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile>(AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile.Instance);
            services.AddOptionProfileValidator<AutoCreateRecurringJobWorkerSwarmHostOptions, AutoCreateRecurringJobWorkerSwarmHostOptionsValidationProfile>();

            // Event handlers
            services.AddEventHandlers();

            return services;
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Lock monitor auto creator
            services.New<LockMonitorAutoCreator>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold).And.WithScope.ForAll;
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<LockMonitorAutoCreator, ColonyCreatedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Deletion daemon auto creator
            services.New<DeletionDaemonAutoCreator>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold).And.WithScope.ForAll;
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<DeletionDaemonAutoCreator, ColonyCreatedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Recurring job worker swarm host auto creator
            services.New<RecurringJobWorkerSwarmHostAutoCreator>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold).And.WithScope.ForAll;
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<RecurringJobWorkerSwarmHostAutoCreator, ColonyCreatedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

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
