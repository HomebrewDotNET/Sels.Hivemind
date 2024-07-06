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
using Sels.HiveMind.Queue;
using Sels.Core.Async.TaskManagement;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Castle.DynamicProxy;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.DistributedLocking;
using Sels.HiveMind.DistributedLocking.MySql;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        /// <summary>
        /// Adds a HiveMind distributed lock service that places locks using the <see cref="Sels.SQL.QueryBuilder.MySQL.MySql.Functions.GetLock"/> function.
        /// </summary>
        /// <param name="services">Collection to add the service registrations to</param>
        /// <param name="connectionStringFactory">Factory that will be used to get the connectionstring</param>
        /// <returns>Current services for method chaining</returns>
        public static IServiceCollection AddHiveMindMySqlDistributedLockService(this IServiceCollection services, Func<IServiceProvider, string> connectionStringFactory, string? environment = null)
        {
            services.ValidateArgument(nameof(services));
            connectionStringFactory = connectionStringFactory.ValidateArgument(nameof(connectionStringFactory));
            
            environment ??= HiveMindConstants.DefaultEnvironmentName;

            // Dependencies
            services.RegisterConfigurationService();
            services.AddCachedMySqlQueryProvider();
            services.TryAddSingleton<ProxyGenerator>();

            // Add factory
            services.New<IComponentFactory<IDistributedLockService>, HiveMindMySqlDistributedLockServiceFactory>()
                    .ConstructWith(x =>
                    {
                        var connectionString = connectionStringFactory(x);

                        return new HiveMindMySqlDistributedLockServiceFactory(environment,
                                                                              connectionString,
                                                                              x.GetRequiredService<ProxyGenerator>(),
                                                                              x.GetService<ILogger<HiveMindMySqlDistributedLockServiceFactory>>());
                    })
                    .AsSingleton()
                    .Register();

            return services;
        }
        /// <summary>
        /// Adds a HiveMind distributed lock service that places locks using the <see cref="Sels.SQL.QueryBuilder.MySQL.MySql.Functions.GetLock"/> function.
        /// </summary>
        /// <param name="services">Collection to add the service registrations to</param>
        /// <param name="connectionStringFactory">Factory that will be used to get the connectionstring</param>
        /// <returns>Current services for method chaining</returns>
        public static IServiceCollection AddHiveMindMySqlDistributedLockService(this IServiceCollection services, string? connectionStringName = null, string? environment = null)
        {
            services.ValidateArgument(nameof(services));

            environment ??= HiveMindConstants.DefaultEnvironmentName;
            connectionStringName ??= $"HiveMind.{environment}";

            services.AddHiveMindMySqlDistributedLockService(x => x.GetRequiredService<IConfigurationService>().GetConnectionString(connectionStringName), environment);

            return services;
        }
    }
}
