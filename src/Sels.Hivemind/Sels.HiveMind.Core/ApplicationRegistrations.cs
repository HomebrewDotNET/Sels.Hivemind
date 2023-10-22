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
using Sels.HiveMind.Request;
using Sels.HiveMind.Job;
using Sels.Core.ServiceBuilder;

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

            // Mediator
            services.AddNotifier();

            // Options
            services.AddOptions();
            services.AddValidationProfile<HiveMindOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindOptions, HiveMindOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindOptions>(nameof(HiveMindOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            // Client
            services.New<IBackgroundJobClient, BackgroundJobClient>()
                    .AsScoped()
                    .Trace(x => x.Duration.OfAll)
                    .TryRegister();

            // Services
            services.New<IStorageProvider, StorageProvider>()
                    .AsScoped()
                    .Trace(x => x.Duration.OfAll)
                    .TryRegister();
            services.AddValidationProfile<BackgroundJobValidationProfile, string>();
            services.New<IBackgroundJobService, BackgroundJobService>()
                    .AsScoped()
                    .Trace(x => x.Duration.OfAll)
                    .TryRegister();


            return services;
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Regenerate execution id
            services.AddEventListener<ExecutionIdRegenerator, BackgroundJobStateAppliedEvent>(x => x.AsScoped().WithBehaviour(RegisterBehaviour.TryAdd).Trace(x => x.Duration.OfAll));

            // Meta data tagger
            services.BindOptionsFromConfig<JobMetaDataOptions>(nameof(JobMetaDataOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.AddEventListener<MetaDataTagger, BackgroundJobSavingEvent>(x => x.AsScoped().WithBehaviour(RegisterBehaviour.TryAdd).Trace(x => x.Duration.OfAll));

            // Job retry handler
            services.AddValidationProfile<BackgroundJobRetryOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<BackgroundJobRetryOptions, BackgroundJobRetryOptionsValidationProfile>();
            services.BindOptionsFromConfig<BackgroundJobRetryOptions>(nameof(BackgroundJobRetryOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, BackgroundJobRetryHandler>(x => x.AsScoped().WithBehaviour(RegisterBehaviour.TryAdd).Trace(x => x.Duration.OfAll));

            return services;
        }
    }
}
