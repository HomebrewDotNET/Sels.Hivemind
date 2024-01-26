using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.HiveMind;
using Sels.Core.Configuration;
using Sels.Core.Data.FluentMigrationTool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Queue.MySql;
using Sels.HiveMind.Queue;
using Sels.Core.Async.TaskManagement;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Castle.DynamicProxy;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        /// <summary>
        /// Adds a HiveMind job queue that uses a MySQL based database.
        /// </summary>
        /// <param name="services">Collection to add the service registrations to</param>
        /// <param name="options">Optional options for configuring the storage</param>
        /// <returns>Current services for method chaining</returns>
        public static IServiceCollection AddHiveMindMySqlQueue(this IServiceCollection services, Action<IHiveMindMySqlQueueRegistrationOptions> options = null)
        {
            services.ValidateArgument(nameof(services));

            var registrationsOptions = new RegistrationOptions(options);

            // Dependencies
            services.RegisterConfigurationService();
            services.AddCachedMySqlQueryProvider();
            services.AddOptions();
            services.AddMigrationToolFactory();
            services.TryAddSingleton<ProxyGenerator>();

            // Options
            services.AddValidationProfile<HiveMindMySqlQueueOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindMySqlQueueOptions, HiveMindMySqlQueueOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindMySqlQueueOptions>(nameof(HiveMindMySqlQueueOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);
            if (registrationsOptions.Options != null)
            {
                services.Configure<HiveMindMySqlQueueOptions>(registrationsOptions.Environment, registrationsOptions.Options);
            }

            // Add factory
            services.New<IJobQueueFactory, HiveMindMySqlQueueFactory>()
                    .ConstructWith(x =>
                    {
                        return new HiveMindMySqlQueueFactory(registrationsOptions.Environment,
                                                               registrationsOptions.ConnectionStringFactory(x),
                                                               x.GetRequiredService<IOptionsMonitor<HiveMindMySqlQueueOptions>>(),
                                                               x.GetRequiredService<ProxyGenerator>(),
                                                               x.GetRequiredService<IServiceProvider>(),
                                                               x.GetRequiredService<ITaskManager>(),
                                                               x.GetRequiredService<IMigrationToolFactory>(),
                                                               x.GetService<ILogger<HiveMindMySqlQueueFactory>>());
                    })
                    .AsSingleton()
                    .Register();

            return services;
        }

        private class RegistrationOptions : IHiveMindMySqlQueueRegistrationOptions
        {
            // Properties
            public string Environment { get; private set; } = HiveMindConstants.DefaultEnvironmentName;
            public Func<IServiceProvider, string> ConnectionStringFactory { get; private set; }
            public Action<HiveMindMySqlQueueOptions> Options { get; private set; }

            public RegistrationOptions(Action<IHiveMindMySqlQueueRegistrationOptions> configurator = null)
            {
                UseConnectionString(x =>
                {
                    var configService = x.GetRequiredService<IConfigurationService>();
                    return configService.GetConnectionString($"HiveMind.{Environment}.Storage");
                });
                configurator?.Invoke(this);
            }

            /// <inheritdoc/>
            public IHiveMindMySqlQueueRegistrationOptions ForEnvironment(string environment)
            {
                HiveMindHelper.Validation.ValidateEnvironment(environment);
                Environment = environment;
                return this;
            }
            /// <inheritdoc/>
            public IHiveMindMySqlQueueRegistrationOptions UseConnectionString(Func<IServiceProvider, string> connectionStringFactory)
            {
                ConnectionStringFactory = connectionStringFactory.ValidateArgument(nameof(connectionStringFactory));
                return this;
            }
            /// <inheritdoc/>
            public IHiveMindMySqlQueueRegistrationOptions ConfigureOptions(Action<HiveMindMySqlQueueOptions> options)
            {
                options.ValidateArgument(nameof(options));

                if (Options == null) Options = options;
                else Options += options;
                return this;
            }
        }
    }
    /// <summary>
    /// Exposes more options for adding a MySQL based storage for HiveMind.
    /// </summary>
    public interface IHiveMindMySqlQueueRegistrationOptions
    {
        /// <summary>
        /// Configures the storage for HiveMind environment with name <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The name of the HiveMind environment to register the storage for</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlQueueRegistrationOptions ForEnvironment(string environment);
        /// <summary>
        /// Defines a delegate that returns the connection string for the storage.
        /// </summary>
        /// <param name="connectionStringFactory">Delegate that returns the connection string for the storage</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlQueueRegistrationOptions UseConnectionString(Func<IServiceProvider, string> connectionStringFactory);
        /// <summary>
        /// Defines the connection string for the storage.
        /// </summary>
        /// <param name="connectionString">The connection string for the storage</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlQueueRegistrationOptions UseConnectionString(string connectionString) => UseConnectionString(x => connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString)));
        /// <summary>
        /// Configures the options for the storage using <paramref name="options"/>.
        /// </summary>
        /// <param name="options">The delegate that will configure the options.</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlQueueRegistrationOptions ConfigureOptions(Action<HiveMindMySqlQueueOptions> options);
    }
}
