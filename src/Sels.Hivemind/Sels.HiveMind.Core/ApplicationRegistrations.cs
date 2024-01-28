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
using Sels.HiveMind.Scheduler.Lazy;
using Sels.HiveMind.Service;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        /// <summary>
        /// Adds all the core services needed by HiveMind.
        /// </summary>
        /// <param name="services">Collection to add the services to</param>
        /// <returns><paramref name="services"/> for method chaining</returns>
        public static IServiceCollection AddHiveMind(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Event/request handlers
            services.AddEventHandlers();

            // Schedulers
            services.AddSchedulers();

            // Mediator
            services.AddNotifier();

            // Options
            services.AddOptions();
            services.AddValidationProfile<HiveMindOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindOptions, HiveMindOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindOptions>(nameof(HiveMindOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);
            
            services.AddValidationProfile<HiveMindLoggingOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindLoggingOptions, HiveMindLoggingOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindLoggingOptions>(nameof(HiveMindLoggingOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.Ignore, false);

            // Client
            services.New<IBackgroundJobClient, BackgroundJobClient>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ClientWarningThreshold, options.ClientErrorThreshold);
                    })
                    .TryRegister();

            // Services
            services.New<IStorageProvider, StorageProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.New<IJobQueueProvider, JobQueueProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.New<IJobSchedulerProvider, JobSchedulerProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.AddValidationProfile<BackgroundJobValidationProfile, string>();
            services.AddValidationProfile<BackgroundJobQueryValidationProfile, string>();
            services.New<IBackgroundJobService, BackgroundJobService>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();


            return services;
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Meta data tagger
            services.BindOptionsFromConfig<JobMetaDataOptions>(nameof(JobMetaDataOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.New<MetaDataTagger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<MetaDataTagger, BackgroundJobSavingEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Job retry handler
            services.AddValidationProfile<BackgroundJobRetryOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<BackgroundJobRetryOptions, BackgroundJobRetryOptionsValidationProfile>();
            services.BindOptionsFromConfig<BackgroundJobRetryOptions>(nameof(BackgroundJobRetryOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.New<BackgroundJobRetryHandler>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, BackgroundJobRetryHandler>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Background job process trigger
            services.New<BackgroundJobProcessTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<BackgroundJobProcessTrigger, BackgroundJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddEventListener<BackgroundJobProcessTrigger, BackgroundJobLockTimedOutEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Background job cleanup trigger
            services.New<BackgroundJobCleanupTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<BackgroundJobCleanupTrigger, BackgroundJobStateAppliedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddEventListener<BackgroundJobCleanupTrigger, BackgroundJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Background job awaiting handler
            services.New<BackgroundJobAwaitingProcessTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<BackgroundJobAwaitingProcessTrigger, BackgroundJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, BackgroundJobAwaitingProcessTrigger>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            return services;
        }

        private static IServiceCollection AddSchedulers(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Lazy 
            services.AddValidationProfile<LazySchedulerOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<LazySchedulerOptions, LazySchedulerOptionsValidationProfile>();
            services.BindOptionsFromConfig<LazySchedulerOptions>(nameof(LazySchedulerOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.New<IJobSchedulerFactory, LazySchedulerFactory>()
                    .AsScoped()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegisterImplementation();

            return services;
        }
    }
}
