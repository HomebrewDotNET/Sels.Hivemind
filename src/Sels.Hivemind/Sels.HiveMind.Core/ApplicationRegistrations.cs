using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.HiveMind;
using Sels.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Client;
using Sels.HiveMind.Service.Job;
using Sels.HiveMind.Validation;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        public static IServiceCollection AddHiveMind(this IServiceCollection services, Action<HiveMindOptions>? options = null)
        {
            services.ValidateArgument(nameof(services));

            // Mediator
            services.AddNotifier();

            // Options
            services.AddOptions();
            services.AddValidationProfile<HiveMindOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindOptions, HiveMindOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindOptions>(nameof(HiveMindOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);
            if (options != null) services.Configure<HiveMindOptions>(options);

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
    }
}
