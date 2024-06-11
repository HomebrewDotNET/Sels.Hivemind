using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.ServiceBuilder;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.State.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    public static class CoreExamples
    {
        internal static async Task RunAllAsync(CancellationToken token)
        {
            Configure();

            await RunTransaction(token);
            AddRequestHandler();

            await ConfigureEnvironments(token);
        }

        internal static void Configure()
        {
            var services = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue(x => x.ForEnvironment("Custom"))
                               .AddHiveMindMySqlStorage(x => x.ForEnvironment("Custom")); // Register services using IServiceCollection

            services.AddLogging(x => x.AddConsole()); // Native support for ILogger
            services.Configure<HiveMindOptions>("Custom", x => x.CompletedBackgroundJobRetention = TimeSpan.FromDays(1)); // Configure options per environment using IOptions<T> framework. Also supports appsettings configuration.
            services.AddAutofac(); // Native support for IServiceProvider
        }

        internal static async Task RunTransaction(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            var jobId = await client.CreateAsync<ConsoleWriterJob>(x => x.RunAsync(default, "", default), x => x.InState(new FailedState()), token: token);

            await using (var connection = await client.OpenConnectionAsync(startTransaction: true, token))
            {
                await using var failedJob = await client.GetWithLockAsync(connection, jobId, "ResubmitHandler", token);

                if (failedJob.State is FailedState && await failedJob.ChangeStateAsync(connection, new DeletedState() { Reason = "Job is being resubmitted" }, token))
                {
                    var newJobId = await client.CreateAsync<ConsoleWriterJob>(connection, x => x.RunAsync(default, "", default), token: token);
                    _ = await client.CreateAsync<ConsoleWriterJob>(connection, x => x.RunAsync(default, "", default), x => x.EnqueueAfter(newJobId, BackgroundJobContinuationStates.Succeeded), token: token);

                    // Todo add recurring job

                    await connection.CommitAsync(token);
                }
            }
        }

        internal static void AddRequestHandler()
        {
            var services = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage();

            services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, TenantPaymentEnforcer>(x => x.AsScoped().WithBehaviour(RegisterBehaviour.TryAddImplementation));
        }

        internal static async Task ConfigureEnvironments(CancellationToken token)
        {
            const string TenantAEnvironment = "TenantA";
            const string TenantBEnvironment = "TenantB";
            const string MachineEnvironment = "Machine";
            const string ProcessEnvironment = "Process";

            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue(x => x.ForEnvironment(TenantAEnvironment))
                               .AddHiveMindMySqlStorage(x => x.ForEnvironment(TenantAEnvironment))
                               .AddHiveMindMySqlQueue(x => x.ForEnvironment(TenantBEnvironment))
                               .AddHiveMindMySqlStorage(x => x.ForEnvironment(TenantBEnvironment))
                               .AddHiveMindSqliteQueue(MachineEnvironment)
                               .AddHiveMindSqliteStorage(MachineEnvironment)
                               .AddHiveMindSqliteQueue(ProcessEnvironment)
                               .AddHiveMindSqliteStorage(ProcessEnvironment)
                               .Configure<HiveMindOptions>(ProcessEnvironment, x => x.CompletedBackgroundJobRetention = TimeSpan.Zero) // Different options per environment
                               .Configure<HiveMindOptions>(MachineEnvironment, x => x.CompletedBackgroundJobRetention = TimeSpan.FromDays(1))
                               .Configure<HiveMindOptions>(TenantAEnvironment, x => x.CompletedBackgroundJobRetention = TimeSpan.FromDays(7))
                               .Configure<HiveMindOptions>(TenantBEnvironment, x => x.CompletedBackgroundJobRetention = TimeSpan.FromDays(14))
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();
            
            // Enqueue jobs in each environment
            _ = await client.CreateAsync<ConsoleWriterJob>(TenantAEnvironment, x => x.RunAsync(default, "I'm a job for tenant A", default), token: token);
            _ = await client.CreateAsync<ConsoleWriterJob>(TenantBEnvironment, x => x.RunAsync(default, "I'm a job for tenant B", default), token: token);
            _ = await client.CreateAsync<ConsoleWriterJob>(MachineEnvironment, x => x.RunAsync(default, "I'm a job scoped to the host", default), token: token);
            _ = await client.CreateAsync<ConsoleWriterJob>(ProcessEnvironment, x => x.RunAsync(default, "I'm a job scoped to the current process", default), token: token);
        }
    }
}
