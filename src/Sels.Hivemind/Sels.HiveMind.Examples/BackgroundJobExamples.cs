using Microsoft.Extensions.DependencyInjection;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    public static class BackgroundJobExamples
    {
        internal static async Task RunAllAsync(CancellationToken token)
        {
            await Create(token);
            await CreateInQueues(token);
            await CreateDelayed(token);
            await CreateAwaiting(token);
            await CreateWithMiddleware(token);
            await Query(token);
            await ChangeState(token);
            await ScheduleCustomJob(token);
        }

        internal static async Task Create(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();    

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            var jobId = await client.CreateAsync<ConsoleWriterJob>(x => x.Run(default(IBackgroundJobExecutionContext), "I'm a job argument!", default(CancellationToken)), token: token);  
            _ = await client.CreateAsync<ConsoleWriterJob>(x => x.RunAsync(default(IBackgroundJobExecutionContext), "I'm a job argument to an async job that returns a value!", default(CancellationToken)), token: token);
            _ = await client.CreateAsync(() => HelloWorld("I'm a job parameter to a static method!"), token: token);
        }

        internal static async Task CreateInQueues(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            _ = await client.CreateAsync(() => DoStuff(), x => x.WithPriority(QueuePriority.Critical), token: token);
            _ = await client.CreateAsync(() => DoStuff(), x => x.InQueue("Finalize", QueuePriority.High), token: token);
            _ = await client.CreateAsync(() => DoStuff(), x => x.InQueue("Process", QueuePriority.Normal), token: token);
            _ = await client.CreateAsync(() => DoStuff(), x => x.InQueue("Initialize", QueuePriority.Low), token: token);
            _ = await client.CreateAsync(() => DoStuff(), x => x.InQueue("Maintenance", QueuePriority.None), token: token);
        }

        internal static async Task CreateDelayed(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            _ = await client.CreateAsync(() => DoStuff(), x => x.DelayExecutionTo(DateTime.UtcNow.AddHours(8)), token: token);
            _ = await client.CreateAsync(() => DoStuff(), x => x.DelayExecutionTo(DateTime.UtcNow.AddMinutes(5)), token: token);
            _ = await client.CreateAsync(() => DoStuff(), x => x.DelayExecutionTo(DateTime.UtcNow.AddSeconds(1)), token: token);
        }

        internal static async Task CreateAwaiting(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            await using (var connection = await client.OpenConnectionAsync(true, token))
            {
                var firstJobId = await client.CreateAsync(connection, () => DoStuff(), token: token);
                var secondJobId = await client.CreateAsync(connection, () => DoStuff(), x => x.EnqueueAfter(firstJobId, BackgroundJobContinuationStates.Succeeded), token: token);
                var thirdJobId = await client.CreateAsync(connection, () => DoStuff(), x => x.EnqueueAfter(secondJobId, BackgroundJobContinuationStates.Any), token: token);
                _ = await client.CreateAsync(connection, () => DoStuff(), x => x.EnqueueAfter(thirdJobId, BackgroundJobContinuationStates.Finished), token: token);

                await connection.CommitAsync(token);
            }
        }

        internal static async Task CreateWithMiddleware(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            _ = await client.CreateAsync(() => DoStuff(), x => x.WithMiddleWare<BackgroundJobExampleMiddleware>("I'm input for the middleware", priority: 12), token: token);
        }

        internal static async Task Query(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();
            var tenantId = Guid.NewGuid();

            _ = await client.CreateAsync(() => DoStuff(), x => x.WithProperty(nameof(tenantId), tenantId), token: token); // Create job for the tenant
            var amountOfJobsForTenant = await client.CountAsync(x => x.Property(nameof(tenantId)).AsGuid.EqualTo(tenantId)); // Get amount of jobs created for the tenant
            await using var tenantJobs = await client.SearchAsync(x => x.Property(nameof(tenantId)).AsGuid.EqualTo(tenantId), 
                                                                  pageSize: 100, 
                                                                  page: 1, 
                                                                  orderBy: QueryBackgroundJobOrderByTarget.CreatedAt, 
                                                                  orderByDescending: true, 
                                                                  token: token); // Search for first 100 jobs created for the tenant
        }

        internal static async Task ChangeState(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();
            
            var jobId = await client.CreateAsync(() => DoStuff(), x => x.InState(new IdleState()), token: token); // Create idle job

            await using (var job = await client.GetWithLockAsync(jobId, requester: "SomeProcessId", token: token)) // Get job with write lock
            {
                await job.ChangeStateAsync(new EnqueuedState(), token); // Manually enqueue job
                await job.SaveChangesAsync(retainLock: false, token); // Save changes and release lock
            }
        }

        internal static async Task ScheduleCustomJob(CancellationToken token)
        {
            var provider = new ServiceCollection()
                               .AddHiveMind()
                               .AddHiveMindMySqlQueue()
                               .AddHiveMindMySqlStorage()
                               .BuildServiceProvider();

            var client = provider.GetRequiredService<IBackgroundJobClient>();

            _ = await client.CreateAsync<BackgroundJobExample>(x => x.ExecuteAsync(default(IBackgroundJobExecutionContext), new BackgroundJobExampleInput() { EntityId = Guid.NewGuid().ToString() }, default(CancellationToken)), token: token);
        }

        public static void HelloWorld(string message)
        {
            Console.WriteLine(message);
        }

        public static void DoStuff()
        {

        }
    }
}
