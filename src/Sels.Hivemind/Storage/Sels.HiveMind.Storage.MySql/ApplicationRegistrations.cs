﻿using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.HiveMind;
using Sels.Core.Configuration;
using Sels.HiveMind.Storage.MySql;
using Sels.Core.Data.FluentMigrationTool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sels.Core.Extensions.Conversion;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        /// <summary>
        /// Adds a HiveMind storage that uses a MySQL based database.
        /// </summary>
        /// <param name="services">Collection to add the service registrations to</param>
        /// <param name="options">Optional options for configuring the storage</param>
        /// <returns>Current services for method chaining</returns>
        public static IServiceCollection AddHiveMindMySqlStorage(this IServiceCollection services, Action<IHiveMindMySqlStorageRegistrationOptions> options = null)
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
            services.AddValidationProfile<HiveMindMySqlStorageOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindMySqlStorageOptions, HiveMindMySqlStorageOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindMySqlStorageOptions>(nameof(HiveMindMySqlStorageOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);
            if (registrationsOptions.Options != null)
            {
                services.Configure<HiveMindMySqlStorageOptions>(registrationsOptions.Environment, registrationsOptions.Options);
            }

            // Add factory
            services.New<IComponentFactory<IStorage>, HiveMindMySqlStorageFactory>()
                    .ConstructWith(x =>
                    {
                        return new HiveMindMySqlStorageFactory(registrationsOptions.Environment,
                                                               registrationsOptions.ConnectionStringFactory(x),
                                                               x.GetRequiredService<IOptionsMonitor<HiveMindMySqlStorageOptions>>(),
                                                               x.GetRequiredService<ProxyGenerator>(),
                                                               x.GetRequiredService<IMigrationToolFactory>(),
                                                               x.GetService<ILogger<HiveMindMySqlStorageFactory>>());
                    })
                    .AsSingleton()
                    .Register();

            return services;
        }

        private class RegistrationOptions : IHiveMindMySqlStorageRegistrationOptions
        {
            // Properties
            public string Environment { get; private set; } = HiveMindConstants.DefaultEnvironmentName;
            public Func<IServiceProvider, string> ConnectionStringFactory { get; private set; }
            public Action<HiveMindMySqlStorageOptions> Options { get; private set; }

            public RegistrationOptions(Action<IHiveMindMySqlStorageRegistrationOptions> configurator = null)
            {
                this.CastTo<IHiveMindMySqlStorageRegistrationOptions>().UseConnectionStringName($"HiveMind.{Environment}");
                configurator?.Invoke(this);
            }

            /// <inheritdoc/>
            public IHiveMindMySqlStorageRegistrationOptions ForEnvironment(string environment)
            {
                HiveMindHelper.Validation.ValidateEnvironment(environment);
                Environment = environment;
                return this;
            }
            /// <inheritdoc/>
            public IHiveMindMySqlStorageRegistrationOptions UseConnectionString(Func<IServiceProvider, string> connectionStringFactory)
            {
                ConnectionStringFactory = connectionStringFactory.ValidateArgument(nameof(connectionStringFactory));
                return this;
            }
            /// <inheritdoc/>
            public IHiveMindMySqlStorageRegistrationOptions ConfigureOptions(Action<HiveMindMySqlStorageOptions> options)
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
    public interface IHiveMindMySqlStorageRegistrationOptions
    {
        /// <summary>
        /// Configures the storage for HiveMind environment with name <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">The name of the HiveMind environment to register the storage for</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlStorageRegistrationOptions ForEnvironment(string environment);
        /// <summary>
        /// Defines a delegate that returns the connection string for the storage.
        /// </summary>
        /// <param name="connectionStringFactory">Delegate that returns the connection string for the storage</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlStorageRegistrationOptions UseConnectionString(Func<IServiceProvider, string> connectionStringFactory);
        /// <summary>
        /// Defines the connection string for the storage.
        /// </summary>
        /// <param name="connectionString">The connection string for the storage</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlStorageRegistrationOptions UseConnectionString(string connectionString) => UseConnectionString(x => connectionString.ValidateArgumentNotNullOrWhitespace(nameof(connectionString)));
        /// <summary>
        /// Defines the connection string name ot use when reading from config
        /// </summary>
        /// <param name="connectionStringName">The connection string name to read from config</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlStorageRegistrationOptions UseConnectionStringName(string connectionStringName) => UseConnectionString(x =>
        {
            var configService = x.GetRequiredService<IConfigurationService>();
            return configService.GetConnectionString(connectionStringName.ValidateArgumentNotNullOrWhitespace(nameof(connectionStringName)));
        });
        /// <summary>
        /// Configures the options for the storage using <paramref name="options"/>.
        /// </summary>
        /// <param name="options">The delegate that will configure the options.</param>
        /// <returns>Current options for method chaining</returns>
        IHiveMindMySqlStorageRegistrationOptions ConfigureOptions(Action<HiveMindMySqlStorageOptions> options);
    }
}
