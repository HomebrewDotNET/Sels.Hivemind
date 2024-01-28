using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    internal static class FakeExtensions
    {
        public static IServiceCollection AddHiveMindSqliteQueue(this IServiceCollection services, string environment)
        {
            return services.AddHiveMindMySqlQueue(x => x.ForEnvironment(environment).UseConnectionStringName("HiveMind.Main"));
        }

        public static IServiceCollection AddHiveMindSqliteStorage(this IServiceCollection services, string environment)
        {
            return services.AddHiveMindMySqlStorage(x => x.ForEnvironment(environment).UseConnectionStringName("HiveMind.Main"));
        }

        public static IServiceCollection AddHiveMindSqliteMemoryQueue(this IServiceCollection services, string environment)
        {
            return services.AddHiveMindMySqlQueue(x => x.ForEnvironment(environment).UseConnectionStringName("HiveMind.Main"));
        }

        public static IServiceCollection AddHiveMindSqliteMemoryStorage(this IServiceCollection services, string environment)
        {
            return services.AddHiveMindMySqlStorage(x => x.ForEnvironment(environment).UseConnectionStringName("HiveMind.Main"));
        }
    }
}
