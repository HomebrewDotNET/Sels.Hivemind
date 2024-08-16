using Castle.Core.Logging;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.Core.ServiceBuilder.Interceptors;
using Sels.HiveMind;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Colony.Options;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Swarm.Job.Background;
using Sels.HiveMind.Colony.SystemDaemon;
using Sels.HiveMind.Examples;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.State.Background;
using Sels.HiveMind.Job.State.Recurring;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Queue.MySql;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.MySql;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Xml.Schema;
using static Sels.HiveMind.HiveMindConstants;

await Helper.Console.RunAsync(async () =>
{
    //await Actions.CreateRecurringJobsAsync();
    await Actions.RunAndSeedColony(8, SeedType.Plain, 16, TimeSpan.FromSeconds(1));
    //await Actions.CreateJobsAsync();
    //await Actions.Test();
    //await Actions.QueryJobsAsync();
});

public static class Actions
{
    public static async Task RunAndSeedColony(int seeders, SeedType type, int drones, TimeSpan monitorInterval)
    {
        await using var provider = new ServiceCollection()
                            .AddHiveMindColony()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddHiveMindMySqlDistributedLockService()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter("Sels.Hivemind", LogLevel.Error);
                                //x.AddFilter("Sels.Hivemind.Colony", LogLevel.Information);
                                x.AddFilter("Sels.Core.ServiceBuilder.Interceptors", LogLevel.Error);
                                //TracingInterceptor.LongRunningOffset = TimeSpan.FromMilliseconds(200);
                            })
                            .Configure<HiveMindOptions>("Main", x => x.CompletedBackgroundJobRetention = TimeSpan.FromMinutes(1))
                            //.Configure<WorkerSwarmDefaultHostOptions>(o => o.LogLevel = LogLevel.Information)
                            //.Configure<HiveMindMySqlStorageOptions>("Main", o =>
                            //{
                            //    o.PerformanceWarningThreshold = TimeSpan.FromMilliseconds(30);
                            //    o.PerformanceErrorThreshold = TimeSpan.FromMilliseconds(50);
                            //})
                            //.Configure<HiveMindMySqlQueueOptions>("Main", o =>
                            //{
                            //    o.PerformanceWarningThreshold = TimeSpan.FromMilliseconds(30);
                            //    o.PerformanceErrorThreshold = TimeSpan.FromMilliseconds(50);
                            //})
                            .Configure<HiveMindLoggingOptions>(o =>
                            {
                                //o.EventHandlersWarningThreshold = TimeSpan.FromMilliseconds(30);
                                //o.EventHandlersErrorThreshold = TimeSpan.FromMilliseconds(50);
                                // o.ServiceWarningThreshold = TimeSpan.FromMilliseconds(30);
                                //o.ServiceErrorThreshold = TimeSpan.FromMilliseconds(100);
                                //o.ClientWarningThreshold = TimeSpan.FromMilliseconds(100);
                                //o.ClientErrorThreshold = TimeSpan.FromMilliseconds(200);
                            })
                            .Configure<DeletionDeamonOptions>("Main.$DeletionDaemon", x =>
                            {
                                x.Drones = 1;
                            })
                            .BuildServiceProvider();

        var colonyFactory = provider.GetRequiredService<IColonyFactory>();
        var logger = provider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger<Program>();
        var token = Helper.App.ApplicationToken;

        await using (var colony = await colonyFactory.CreateAsync(x =>
        {
            x.WithRecurringJobWorkerSwarm("Main", swarmBuilder: x =>
            {
                x.Drones = 1;
                x.DroneAlias = "Magos";
                x.UseRomanIdGenerator();
            }).WithWorkerSwarm("Main", swarmBuilder: x =>
            {
                x.Drones = drones - 2;
                x.Drones = x.Drones < 0 ? 0 : x.Drones;
                x.DroneAlias = "Servitor";
                x.UseRomanIdGenerator()
                .AddQueue("Process")
                .UsePullthroughScheduler(x => x.PrefetchMultiplier = 5)
                .AddSubSwarm("InitializerAndFinalizer", x =>
                {
                    x.Drones = drones > 0 ? 1 : 0;
                    x.DroneAlias = "TechPriest";
                    x.UseHexadecimalIdGenerator(3)
                     .AddQueue("Initialize", 2)
                     .AddQueue("Finalize", 1)
                     .UseQueueDistributedLocking();
                })
                .AddSubSwarm("LongRunning", x =>
                {
                    x.Drones = drones > 1 ? 1 : 0;
                    x.DroneAlias = "ServoSkull";
                    x.UseAlpabetIdGenerator()
                     .AddQueue("LongRunning")
                     .AddJobMiddleware<BackgroundJobExampleMiddleware>(x => new ScopedComponent<BackgroundJobExampleMiddleware>("ExampleMiddleware", new BackgroundJobExampleMiddleware(), null, false).ToTaskResult<IComponent<BackgroundJobExampleMiddleware>>());
                });
            })
            .WithOptions(new HiveColonyOptions()
            {
                DefaultDaemonLogLevel = LogLevel.Warning,
                CreationOptions = HiveColonyCreationOptions.Default
            });
            if (monitorInterval > TimeSpan.Zero) x.WithDaemon("Monitor", (c, t) => DisplayProcessingOverview(c, monitorInterval, t), x => x.WithPriority(1).WithRestartPolicy(DaemonRestartPolicy.OnFailure));
            Enumerable.Range(0, seeders).Execute(s =>
            {
                x.WithDaemon($"Seeder.{s}", async (c, t) =>
                {
                    var priorities = Helper.Enums.GetAll<QueuePriority>();
                    var queues = new string[] { "Global", "Process" };
                    var rareQueues = new string[] { "Initialize", "Finalize", "LongRunning" };
                    var client = c.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

                    await using (var connection = await client.OpenConnectionAsync(false, t).ConfigureAwait(false))
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                            if (type.HasFlag(SeedType.Hello))
                            {
                                var jobId = await client.CreateAsync(connection, () => Hello(null, $"Hello from daemon <{c.Daemon.Name}> in colony <{c.Daemon.Colony.Name}>"), x => x.InQueue("Initialize", priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => HelloAsync(null, $"Hello async from daemon <{c.Daemon.Name}> in colony <{c.Daemon.Colony.Name}>", default), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Any).InQueue("Process", priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => DoStuff(null, $"Doing stuff from daemon <{c.Daemon.Name}> in colony <{c.Daemon.Colony.Name}>"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Executing).InQueue("Finalize", priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => Hello(null, $"Hello from daemon <{c.Daemon.Name}> in colony <{c.Daemon.Colony.Name}>"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Succeeded).WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                            }
                            if (type.HasFlag(SeedType.Data))
                            {
                                _ = await client.CreateAsync(connection, () => Save<string>(null, $"Generated from from daemon <{c.Daemon.Name}> in colony <{c.Daemon.Colony.Name}>", default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => Save<double>(null, Helper.Random.GetRandomDouble(0, 100), default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => Save<int>(null, Helper.Random.GetRandomInt(0, 100), default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => Save<HiveMindOptions>(null, new HiveMindOptions(), default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => Save<short>(null, new short[] { 1, 2, 3, 4, 5 }, default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => JobActions<string>.Save(null, $"Generated from from daemon <{c.Daemon.Name}> in colony <{c.Daemon.Colony.Name}>", default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => JobActions<double>.Save(null, Helper.Random.GetRandomDouble(0, 100), default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => JobActions<int>.Save(null, Helper.Random.GetRandomInt(0, 100), default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => JobActions<HiveMindOptions>.Save(null, new HiveMindOptions(), default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                                _ = await client.CreateAsync(connection, () => JobActions<short>.Save(null, new short[] { 1, 2, 3, 4, 5 }, default), x => x.WithPriority(priorities.GetRandomItem()), t).ConfigureAwait(false);
                            }

                            if (type.HasFlag(SeedType.Plain))
                            {
                                bool fromRare = Helper.Random.GetRandomInt(0, 10) > 9;
                                _ = await client.CreateAsync(() => Actions.Nothing(), x => x.WithPriority(priorities.GetRandomItem()).InQueue(fromRare ? rareQueues.GetRandomItem() : queues.GetRandomItem()), t).ConfigureAwait(false);
                            }

                            if (type.HasFlag(SeedType.LongRunning))
                            {
                                _ = await client.CreateAsync(() => Actions.Delay(TimeSpan.FromMinutes(2), default), x => x.InQueue("LongRunning", priorities.GetRandomItem()), t).ConfigureAwait(false);
                            }

                            await connection.CommitAsync(token).ConfigureAwait(false);
                        }
                    }
                }, b => b.WithPriority(250)
                         .WithRestartPolicy(DaemonRestartPolicy.OnFailure));
            });

            if (type.HasFlag(SeedType.LongRunning))
            {
                x.WithDaemon("Canceller", async (x, t) =>
                {
                    var client = x.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
                    while (!t.IsCancellationRequested)
                    {
                        await Helper.Async.Sleep(monitorInterval).ConfigureAwait(false);
                        if (t.IsCancellationRequested) return;

                        await using (var result = await client.SearchAsync(x => x.Queue.EqualTo("LongRunning")
                                                                                .And.CurrentState.Name.EqualTo(ExecutingState.StateName)).ConfigureAwait(false))
                        {
                            foreach (var job in result.Results)
                            {
                                await job.CancelAsync(x.Daemon.Name, "Cancelled by daemon", t).ConfigureAwait(false);
                            }
                        }
                    }
                }, x => x.WithPriority(ushort.MaxValue).WithRestartPolicy(DaemonRestartPolicy.OnFailure));
            }
        }))
        {
            using var tokenRegistration = token.Register(() => logger.Log($"Received cancellation request"));
            await colony.StartAsync(token);

            await Helper.Async.WaitUntilCancellation(token);
        }
    }

    public static async Task CreateJobsAsync()
    {
        await using var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Critical);
                                x.AddFilter("Sels.HiveMind.Queue.MySql", LogLevel.Error);
                                x.AddFilter("Sels.HiveMind.Storage.MySql", LogLevel.Error);
                                x.AddFilter("Sels.HiveMind", LogLevel.Error);
                                x.AddFilter("Program", LogLevel.Information);
                                x.AddFilter("Actions", LogLevel.Information);
                            })
                            //.Configure<HiveMindMySqlStorageOptions>("Main", o =>
                            //{
                            //    o.PerformanceWarningThreshold = TimeSpan.FromMilliseconds(1);
                            //    o.PerformanceErrorThreshold = TimeSpan.FromMilliseconds(2);
                            //})
                            //.Configure<HiveMindMySqlStorageOptions>("Main", x =>
                            //{
                            //    x.PerformanceWarningThreshold = TimeSpan.FromMilliseconds(10);
                            //})
                            //.Configure<HiveMindLoggingOptions>(x =>
                            //{
                            //    x.ServiceWarningThreshold = TimeSpan.FromMilliseconds(1);
                            //    x.ClientWarningThreshold = TimeSpan.FromMilliseconds(1);
                            //    x.EventHandlersWarningThreshold = TimeSpan.FromMilliseconds(1);
                            //})
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        var logger = provider.GetRequiredService<ILogger<Program>>();
        string id = null;

        // Create awaiting
        foreach (var i in Enumerable.Range(0, 10))
        {
            string jobId = null;
            using (Helper.Time.CaptureDuration(x => logger.Log($"Created job <{jobId}> with 5 awaiting jobs in <{x.PrintTotalMs()}>")))
            {
                await using (var connection = await client.OpenConnectionAsync(true, Helper.App.ApplicationToken))
                {
                    jobId = await client.CreateAsync(connection, () => Hello(null, $"Hello from iteration {i}"));
                    _ = await client.CreateAsync(connection, () => Hello(null, $"Awaiting 1: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Succeeded | BackgroundJobContinuationStates.Failed));
                    _ = await client.CreateAsync(connection, () => Hello(null, $"Awaiting 2: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Any));
                    _ = await client.CreateAsync(connection, () => Hello(null, $"Awaiting 3: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, null));
                    _ = await client.CreateAsync(connection, () => Hello(null, $"Awaiting 4: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, true, DeletedState.StateName));
                    _ = await client.CreateAsync(connection, () => Hello(null, $"Awaiting 5: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, true, IdleState.StateName));

                    await connection.CommitAsync(Helper.App.ApplicationToken).ConfigureAwait(false);
                }
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Triggered 5 awaiting jobs for job <{jobId}> in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync(jobId, "Me", Helper.App.ApplicationToken))
                {
                    await job.ChangeStateAsync(new IdleState(), Helper.App.ApplicationToken);
                    await job.SaveChangesAsync(Helper.App.ApplicationToken);
                }
            }

            Console.WriteLine();
        }

        // Creates
        foreach (var i in Enumerable.Range(0, 10))
        {
            using (Helper.Time.CaptureDuration(x => logger.Log($"Created job <{id}> in <{x.PrintTotalMs()}>")))
            {
                var message = $"Hello from iteration {i}";
                id = await client.CreateAsync(() => Hello(null, message), x => x.WithProperty("Text", "Hello!")
                                                                          .WithProperty("Number", 1998)
                                                                          .WithProperty("Digit", 12.90)
                                                                          .WithProperty("Date", DateTime.Now)
                                                                          .WithProperty("Other", Array.Empty<string>())
                                             , token: Helper.App.ApplicationToken);
            }
            IBackgroundJobState[] states = null;
            using (Helper.Time.CaptureDuration(x => logger.Log($"Fetched job <{id}> with state history <{states?.Select(x => x.Name).JoinString("=>")}> in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetAsync(id, token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                logger.Debug($"\tJob properties: {Environment.NewLine}{job.Properties.Select(x => $"\t\t{x.Key}: {x.Value}").JoinStringNewLine()}");
                await job.DisposeAsync();
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Fetched job <{id}> with state history <{states.Select(x => x.Name).JoinString("=>")}> with write lock in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken))
                {
                    states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                    logger.Debug($"\tJob properties: {Environment.NewLine}{job.Properties.Select(x => $"\t\t{x.Key}: {x.Value}").JoinStringNewLine()}");
                }
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Updated state on job <{id}> in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken))
                {
                    await job.ChangeStateAsync(new FailedState(new Exception("Some error")), Helper.App.ApplicationToken);
                    job.SetProperty("Text", "Hello again!");
                    job.SetProperty("Number", 1997);
                    job.SetProperty("Digit", 666.666);
                    job.SetProperty("Date", DateTime.Now);
                    job.RemoveProperty("Other");
                    await job.ChangeStateAsync(new DeletedState() { Reason = "Job not needed anymore" }, Helper.App.ApplicationToken);
                    await job.SaveChangesAsync(Helper.App.ApplicationToken);
                };
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Set job <{id}> to executing in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken))
                {
                    await job.ChangeStateAsync(new ExecutingState("Test", "Test", "Test"), Helper.App.ApplicationToken);
                    await job.SaveChangesAsync(Helper.App.ApplicationToken);
                };
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Set job <{id}> to succeeded in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken))
                {
                    await job.ChangeStateAsync(new SucceededState(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150), null), Helper.App.ApplicationToken);
                    await job.SaveChangesAsync(Helper.App.ApplicationToken);
                };
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Fetched job <{id}> with state history <{states?.Select(x => x.Name).JoinString("=>")}> in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetAsync(id, token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                logger.Debug($"\tJob properties: {Environment.NewLine}{job.Properties.Select(x => $"\t\t{x.Key}: {x.Value}").JoinStringNewLine()}");
                await job.DisposeAsync();
            }

            logger.Log($"");
        }
    }

    public static async Task CreateRecurringJobsAsync()
    {
        await using var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Critical);
                                x.AddFilter("Sels.HiveMind.Queue.MySql", LogLevel.Error);
                                x.AddFilter("Sels.HiveMind.Storage.MySql", LogLevel.Error);
                                x.AddFilter("Sels.HiveMind", LogLevel.Error);
                                x.AddFilter("Program", LogLevel.Information);
                                x.AddFilter("Actions", LogLevel.Information);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IRecurringJobClient>();
        var logger = provider.GetRequiredService<ILogger<Program>>();

        // Create and update
        foreach (var i in Enumerable.Range(0, 10))
        {
            var id = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            using (Helper.Time.CaptureDuration(x => logger.Log($"Created 2 recurring jobs <{id}> in <{x.PrintTotalMs()}>")))
            {
                await client.CreateOrUpdateAsync($"TestRecurringJobOne.{id}", () => Hello(null, $"Hello from iteration {i}"), x => x.WithSchedule(b => b.RunEvery(TimeSpan.FromMinutes(5)).OnlyDuring(Calendars.NineToFive).NotDuring(Calendars.Weekend))
                                                                                                                                    .WithProperty("IsManuelDeploy", true), token: Helper.App.ApplicationToken);
                await client.CreateOrUpdateAsync($"TestRecurringJobTwo.{id}", () => Hello(null, $"Hello from iteration {i}"), x => x.WithProperty("IsManuelDeploy", true), token: Helper.App.ApplicationToken);
                await client.CreateOrUpdateAsync($"TestRecurringJobThree.{id}", () => Hello(null, $"Hello from iteration {i}"), x => x.WithProperty("IsManuelDeploy", true), token: Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Updated recurring job <{id}> in <{x.PrintTotalMs()}>")))
            {
                await client.CreateOrUpdateAsync($"TestRecurringJobOne.{id}", () => Hello(null, $"Hello from iteration {i}"), x => x.WithSchedule(b => b.OnlyDuring(Calendars.StartOfMonth).NotDuring(Calendars.WorkWeek))
                                                                                                                                    .WithProperty("TenantId", tenantId)
                                                                                                                                    .InState(new SchedulingState() { Reason = "Manuel requeue" }), token: Helper.App.ApplicationToken);
            }

            IRecurringJobState[] states = null;
            using (Helper.Time.CaptureDuration(x => logger.Log($"Fetched job <{id}> with state history <{states?.Select(x => x.Name).JoinString("=>")}> in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetAsync($"TestRecurringJobOne.{id}", token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                logger.Debug($"\tJob properties: {Environment.NewLine}{job.Properties.Select(x => $"\t\t{x.Key}: {x.Value}").JoinStringNewLine()}");
                await job.DisposeAsync();
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Fetched job <{id}> with state history <{states.Select(x => x.Name).JoinString("=>")}> with write lock in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync($"TestRecurringJobOne.{id}", "Jens", token: Helper.App.ApplicationToken))
                {
                    states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                    logger.Debug($"\tJob properties: {Environment.NewLine}{job.Properties.Select(x => $"\t\t{x.Key}: {x.Value}").JoinStringNewLine()}");
                }
            }

            using (Helper.Time.CaptureDuration(x => logger.Log($"Deleted job <{id}> in <{x.PrintTotalMs()}>")))
            {
                await client.DeleteAsync($"TestRecurringJobTwo.{id}", token: Helper.App.ApplicationToken);
            }

            long total = 0;
            using (Helper.Time.CaptureDuration(x => logger.Log($"Counted <{total}> recurring jobs that are manually deployed in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.Property("IsManuelDeploy").AsBool.EqualTo(true));
            }
            using (Helper.Time.CaptureDuration(x => logger.Log($"Locked <{total}> recurring jobs tied to tenant <{tenantId}> in <{x.PrintTotalMs()}>")))
            {
                await using (var queryResult = await client.SearchAndLockAsync(x => x.Property("TenantId").AsGuid.EqualTo(tenantId)))
                {
                    total = queryResult.Results.Count;
                }
            }
        }
    }
    public static async Task QueryJobsAsync()
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter("Sels.Core.Async", LogLevel.Critical);
                                x.AddFilter("Sels.HiveMind", LogLevel.Critical);
                            })
                            .Configure<HiveMindLoggingOptions>(x =>
                            {
                                x.ClientWarningThreshold = TimeSpan.FromMilliseconds(250);
                                x.ClientErrorThreshold = TimeSpan.FromMilliseconds(500);
                                x.EventHandlersWarningThreshold = TimeSpan.FromSeconds(1);
                                x.EventHandlersErrorThreshold = TimeSpan.FromSeconds(2);
                                x.ServiceWarningThreshold = TimeSpan.FromSeconds(1);
                                x.ServiceErrorThreshold = TimeSpan.FromSeconds(2);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        var logger = provider.GetRequiredService<ILogger<Program>>();

        foreach (var i in Enumerable.Range(0, 100))
        {
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Created batch {i + 1} in <{x.PrintTotalMs()}>")))
            {
                await using (var clientConnection = await client.OpenConnectionAsync("Main", true, Helper.App.ApplicationToken))
                {
                    await client.CreateAsync(clientConnection, () => Hello(null, $"Hello from iteration {i}"), x => x.InState(new IdleState()), Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello(null, $"Hello from iteration {i}"), token: Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello(null, $"Hello from iteration {i}"), x => x.InState(new FailedState(new SystemException("Something went wrong"))), Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello(null, $"Hello from iteration {i}"), x => x.WithProperty("Index", i), Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello(null, $"Hello from iteration {i}"), x => x.InQueue("Testing", QueuePriority.Critical), Helper.App.ApplicationToken);

                    await clientConnection.CommitAsync(Helper.App.ApplicationToken);
                }
            }
        }

        // Dequeue
        foreach (var i in Enumerable.Range(0, 10))
        {
            int dequeued = 0;

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Dequeued <{dequeued}> failed jobs in <{x.PrintTotalMs()}>")))
            {
                await using var clientConnection = await client.OpenConnectionAsync("Main", true, Helper.App.ApplicationToken);
                var result = await client.SearchAndLockAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName), 10, "Jens", false, QueryBackgroundJobOrderByTarget.ModifiedAt, true, Helper.App.ApplicationToken);
                dequeued = result.Results.Count;
                using (var duration = Helper.Time.CaptureDuration(x => Console.WriteLine($"Commited in <{x.PrintTotalMs()}>")))
                {
                    await clientConnection.CommitAsync(Helper.App.ApplicationToken);
                }
                using (var duration = Helper.Time.CaptureDuration(x => Console.WriteLine($"Disposed result in <{x.PrintTotalMs()}>")))
                {
                    await result.DisposeAsync();
                }

            }
            Console.WriteLine();
        }

        // Query
        foreach (var i in Enumerable.Range(0, 10))
        {
            int queried = 0;

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> deleted jobs in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.CurrentState.Name.EqualTo(DeletedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> idle jobs in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.CurrentState.Name.EqualTo(IdleState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> enqueued jobs in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> failed jobs in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs with index property equal to {i} in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.Property<int>("Index").EqualTo(i), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs in test queue in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.Queue.EqualTo("Testing"), 10, 1, QueryBackgroundJobOrderByTarget.Priority, false, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs without retries in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.SearchAsync(x => x.Property("RetryCount").NotExists, 10, 1, QueryBackgroundJobOrderByTarget.Priority, false, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                }
            }

            Console.WriteLine();
        }

        // Count
        foreach (var i in Enumerable.Range(0, 10))
        {
            long total = 0;

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> deleted jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.CurrentState.Name.EqualTo(DeletedState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> idle jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.CurrentState.Name.EqualTo(IdleState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> enqueued jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> failed jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs that where retried in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.Property(HiveMindConstants.Job.Properties.RetryCount).AsInt.GreaterOrEqualTo(1), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> total jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(token: Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs in Testing queue in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.Queue.EqualTo("Testing"), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs without retries in <{x.PrintTotalMs()}>")))
            {
                total = await client.CountAsync(x => x.Property("RetryCount").NotExists, Helper.App.ApplicationToken);
            }
            Console.WriteLine();
        }
    }

    public static async Task Test()
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter(typeof(HiveMindMySqlStorage).FullName, LogLevel.Warning);
                                x.AddFilter("Sels.Core.ServiceBuilder", LogLevel.Debug);
                            })
                            .Configure<HiveMindMySqlStorageOptions>("Main", o => o.PerformanceWarningThreshold = TimeSpan.FromMilliseconds(1))
                            .BuildServiceProvider();

        var token = Helper.App.ApplicationToken;
        var client = provider.GetRequiredService<IBackgroundJobClient>();

        await using var connection = await client.OpenConnectionAsync(false, token);
        var result = await client.SearchAsync(connection, x => { return null; }, 1, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true);
    }

    public static async Task SeedDatabase(int workers, int batchSize)
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter("Sels.HiveMind", LogLevel.Warning);
                                x.AddFilter("Program", LogLevel.Information);
                            })
                            .Configure<HiveMindLoggingOptions>(x =>
                            {
                                x.ClientWarningThreshold = TimeSpan.FromMilliseconds(250);
                                x.ClientErrorThreshold = TimeSpan.FromMilliseconds(500);
                                x.EventHandlersWarningThreshold = TimeSpan.FromMilliseconds(50);
                                x.EventHandlersErrorThreshold = TimeSpan.FromMilliseconds(100);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        var logger = provider.GetRequiredService<ILogger<Program>>();
        var taskManager = provider.GetRequiredService<ITaskManager>();
        var allPriorities = Helper.Enums.GetAll<QueuePriority>();

        Enumerable.Range(0, workers).Execute(x =>
        {
            var workerId = $"Worker{x}";
            taskManager.TryScheduleAction(client, workerId, false, async t =>
            {
                logger.Log($"Worker <{x}> starting");
                while (!t.IsCancellationRequested)
                {
                    await using (var connection = await client.OpenConnectionAsync(false, t))
                    {
                        using (Helper.Time.CaptureDuration(x => logger.Log($"Worker <{workerId}> created batch of size <{batchSize}> in <{x.PrintTotalMs()}>")))
                        {
                            try
                            {
                                await connection.BeginTransactionAsync(t);
                                int currentSize = 0;
                                while (currentSize < batchSize)
                                {
                                    var jobId = await client.CreateAsync(connection, () => Hello(null, $"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello(null, $"Hello from {Environment.ProcessId} at <{DateTime.Now}> awaiting {jobId}"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Any), t);
                                    _ = await client.CreateAsync(connection, () => Hello(null, $"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("01.Process", allPriorities.GetRandomItem()), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello(null, $"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("02.Process", allPriorities.GetRandomItem()), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello(null, $"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("03.Process", allPriorities.GetRandomItem()), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello(null, $"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("04.Process", allPriorities.GetRandomItem()), token: t);
                                    currentSize++;
                                }

                                await connection.CommitAsync(t);

                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                logger.Log($"{workerId} ran into issue", ex);
                            }
                        }
                    }
                }

                logger.Log($"Worker <{x}> stopping");
            }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepAlive));
        });

        await Helper.Async.WaitUntilCancellation(Helper.App.ApplicationToken);

        await taskManager.StopAllForAsync(client);
    }

    public static async Task DequeueJobs(int workers, int dequeueSize)
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter("Sels.HiveMind", LogLevel.Warning);
                                x.AddFilter("Program", LogLevel.Information);
                            })
                            .BuildServiceProvider();

        var queueProvider = provider.GetRequiredService<IJobQueueProvider>();
        var logger = provider.GetRequiredService<ILogger<Program>>();
        var taskManager = provider.GetRequiredService<ITaskManager>();
        var queues = new string[] { "01.Process", "02.Process", "03.Process", "04.Process", "Global" };

        Enumerable.Range(0, workers).Execute(x =>
        {
            var workerId = $"Worker{x}";
            taskManager.TryScheduleAction(queueProvider, workerId, false, async t =>
            {
                var workerQueues = queues.OrderBy(x => Helper.Random.GetRandomInt(1, 10)).Take(Helper.Random.GetRandomInt(1, queues.Length)).ToArray();
                await using var queueScope = await queueProvider.CreateAsync(HiveMindConstants.DefaultEnvironmentName, Helper.App.ApplicationToken);
                var queue = queueScope.Component;
                logger.Log($"Worker <{x}> starting");
                while (!t.IsCancellationRequested)
                {
                    try
                    {
                        using (Helper.Time.CaptureDuration(x => logger.Log($"Worker <{workerId}> dequeued <{dequeueSize}> jobs from queues <{workerQueues.JoinString('|')}> in <{x.PrintTotalMs()}>")))
                        {
                            await foreach (var dequeued in queue.DequeueAsync(HiveMindConstants.Queue.BackgroundJobProcessQueueType, workerQueues, dequeueSize, Helper.App.ApplicationToken))
                            {
                                //logger.Log($"Dequeued job <{dequeued.JobId}> with priority <{dequeued.Priority}> from queue <{dequeued.Queue}> enqueued at <{dequeued.EnqueuedAtUtc}>");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"{workerId} ran into issue", ex);
                    }
                }

                logger.Log($"Worker <{x}> stopping");
            }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepAlive));
        });

        await Helper.Async.WaitUntilCancellation(Helper.App.ApplicationToken);

        await taskManager.StopAllForAsync(queueProvider);
    }

    public static async Task RunColony(int? drones = null)
    {
        var provider = new ServiceCollection()
                            .AddHiveMindColony()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter("Sels.HiveMind", LogLevel.Warning);
                            })
                            .Configure<BackgroundJobWorkerSwarmHostOptions>(o => o.LogLevel = LogLevel.Warning)
                            .BuildServiceProvider();

        var colonyFactory = provider.GetRequiredService<IColonyFactory>();
        var logger = provider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger<Program>();
        var token = Helper.App.ApplicationToken;

        await using (var colony = await colonyFactory.CreateAsync(x =>
        {
            x.WithWorkerSwarm("Test", x => x.Drones = drones)
             .WithOptions(new HiveColonyOptions()
             {
                 DefaultDaemonLogLevel = LogLevel.Warning
             });
        }))
        {
            await colony.StartAsync(Helper.App.ApplicationToken);

            await Helper.Async.WaitUntilCancellation(Helper.App.ApplicationToken);
        }
    }
    public static async Task MonitorJobsAsync(IDaemonExecutionContext context, TimeSpan interval, CancellationToken cancellationToken)
    {
        var client = context.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

        void AppendSwarmState<T>(StringBuilder builder, ISwarmState<T> swarmState, int currentIndent)
        {
            builder.ValidateArgument(nameof(builder));
            swarmState.ValidateArgument(nameof(swarmState));

            // Append swarm header
            builder.Append('\t', currentIndent).Append('[').Append(swarmState.Name).AppendLine("]");
            if (swarmState.ChildSwarms.HasValue())
            {
                // Append child swarms
                currentIndent++;
                foreach (var childSwarmState in swarmState.ChildSwarms)
                {
                    AppendSwarmState(builder, childSwarmState, currentIndent);
                }
                currentIndent--;
            }
            // Append drone state
            if (swarmState.Drones.HasValue())
            {
                foreach (var droneState in swarmState.Drones)
                {
                    builder.Append('\t', currentIndent).Append("Drone.").Append(droneState.Name).Append('(').Append(droneState.IsProcessing ? "ACTIVE" : "IDLE").Append("): ").AppendLine($"Job={droneState.JobId}|Queue={droneState.JobQueue}|Priority={droneState.JobPriority}|Duration={(droneState.Duration?.TotalMilliseconds ?? 0)}ms");
                }
            }
        }

        await using var connection = await client.OpenConnectionAsync(false, cancellationToken).ConfigureAwait(false);
        long lastSuccess = 0;
        var stopwatch = new Stopwatch();
        while (!cancellationToken.IsCancellationRequested)
        {
            await Helper.Async.Sleep(interval - stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
            stopwatch.Restart();
            if (cancellationToken.IsCancellationRequested) return;


            //var total = await client.QueryCountAsync(connection, token: cancellationToken).ConfigureAwait(false);
            //var idle = await client.QueryCountAsync(connection, x => x.CurrentState.Name.EqualTo(IdleState.StateName), cancellationToken).ConfigureAwait(false);
            var enqueued = await client.CountAsync(connection, x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName), cancellationToken).ConfigureAwait(false);
            //var awaiting = await client.QueryCountAsync(connection, x => x.CurrentState.Name.EqualTo(AwaitingState.StateName), cancellationToken).ConfigureAwait(false);
            //var executing = await client.QueryCountAsync(connection, x => x.CurrentState.Name.EqualTo(ExecutingState.StateName), cancellationToken).ConfigureAwait(false);
            var succeeded = await client.CountAsync(connection, x => x.CurrentState.Name.EqualTo(SucceededState.StateName), cancellationToken).ConfigureAwait(false);
            //var failed = await client.QueryCountAsync(connection, x => x.CurrentState.Name.EqualTo(FailedState.StateName), cancellationToken).ConfigureAwait(false);
            //var deleted = await client.QueryCountAsync(connection, x => x.CurrentState.Name.EqualTo(DeletedState.StateName), cancellationToken).ConfigureAwait(false);
            //var locked = await client.QueryCountAsync(connection, x => x.LockedBy.Not.EqualTo(null), cancellationToken).ConfigureAwait(false);

            //Console.WriteLine($"Background job processing state: Idle={idle}|Success={succeeded}|Executing={executing}|Locked={locked}|Failed={failed}|Deleted={deleted}|Pending={enqueued}|Awaiting={awaiting}|Total=?");
            Console.WriteLine($"<{enqueued}> pending jobs");
            if (lastSuccess != 0)
            {
                Console.WriteLine($"Processed <{succeeded - lastSuccess}> in <{interval}>");
            }
            lastSuccess = succeeded;

            PrintThreads();

            var stateBuilder = new StringBuilder();

            foreach (var daemon in context.Daemon.Colony.Daemons)
            {
                if (daemon.State is ISwarmState<object> swarmState)
                {
                    AppendSwarmState(stateBuilder, swarmState, 0);
                    stateBuilder.AppendLine();
                }
            }

            Console.WriteLine(stateBuilder);
        }
    }
    public static void PrintThreads()
    {
        var currentThreads = ThreadPool.ThreadCount;
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availablePortThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxPortThreads);
        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minPortThreads);
        Console.WriteLine($"Thread pool state: Current={currentThreads}|Worker=({minWorkerThreads}/{availableWorkerThreads}/{maxWorkerThreads})|Io=({minPortThreads}/{availablePortThreads}/{maxPortThreads})");

    }
    public static int Hello(IBackgroundJobExecutionContext context, string message)
    {
        if(context != null) context.Log(message);
        return message.Length;
    }

    public static Task<int> HelloAsync(IBackgroundJobExecutionContext context, string message, CancellationToken token = default)
    {
        context.Log(message);
        return message.Length.ToTaskResult();
    }

    public static Task DoStuff(IBackgroundJobExecutionContext context, string message)
    {
        context.Log($"Drone <{context.Drone}> from swarm <{context.Swarm}> doing stuff: {message}");
        return Task.CompletedTask;
    }

    public static async Task Save<T>(IBackgroundJobExecutionContext context, T data, CancellationToken token = default)
    {
        if (await context.Job.TryGetDataAsync<T>("ProcessingState", token).ConfigureAwait(false) is (true, var savedData))
        {
            context.Log($"Data of type <{data?.GetType()}> was already saved to background job <{HiveLog.Job.IdParam}>. Value is <{savedData}>", context.Job.Id);
        }
        else
        {
            context.Log($"Saving data of type <{data?.GetType()}> to background job <{HiveLog.Job.IdParam}>", context.Job.Id);
            await context.Job.SetDataAsync("ProcessingState", data, token).ConfigureAwait(false);

            throw new Exception("Data was saved but oopsy job crashed");
        }
    }

    public static async Task Save<T>(IBackgroundJobExecutionContext context, IEnumerable<T> data, CancellationToken token = default)
    {
        if (await context.Job.TryGetDataAsync<IEnumerable<T>>("ProcessingState", token).ConfigureAwait(false) is (true, var savedData))
        {
            context.Log($"Data of type <{data?.GetType()}> was already saved to background job <{HiveLog.Job.IdParam}>. Value is <{savedData}>", context.Job.Id);
        }
        else
        {
            context.Log($"Saving data of type <{data?.GetType()}> to background job <{HiveLog.Job.IdParam}>", context.Job.Id);
            await context.Job.SetDataAsync("ProcessingState", data, token).ConfigureAwait(false);

            throw new Exception("Data was saved but oopsy job crashed");
        }
    }

    public static void Nothing() { }

    public static Task Delay(TimeSpan delay, CancellationToken token)
    {
        return Task.Delay(delay, token);
    }

    public static async Task DisplayProcessingOverview(IDaemonExecutionContext context, TimeSpan interval, CancellationToken cancellationToken)
    {
        var backgroundJobClient = context.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        var recurringJobClient = context.ServiceProvider.GetRequiredService<IRecurringJobClient>();
        var queueProvider = context.ServiceProvider.GetRequiredService<IJobQueueProvider>();
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var queue = await queueProvider.CreateAsync(HiveMindConstants.DefaultEnvironmentName, cancellationToken);
            await Helper.Async.Sleep(interval).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return;

            //var backgroundJobQueueStateTask = GetBackgroundJobQueueState(backgroundJobClient, queue.Component, cancellationToken);
            //var recurringJobQueueStateTask = GetRecurringJobQueueState(recurringJobClient, queue.Component, cancellationToken);
            //var backgroundJobQueueState = await backgroundJobQueueStateTask;
            //var recurringJobQueueState = await recurringJobQueueStateTask;
            Console.WriteLine("########## Overview ##########");
            // Thread pool
            Console.WriteLine("##### Thread Pool #####");
            PrintThreads();

            // Daemon state
            Console.WriteLine("##### Daemons #####");
            Console.Write(GetDaemonState(context));

            // Queue state
            //Console.WriteLine("##### Background job queues #####");
            //Console.WriteLine(backgroundJobQueueState);
            //Console.WriteLine("##### Recurring job queues #####");
            //Console.WriteLine(recurringJobQueueState);
        }
    }

    private static async Task<string> GetBackgroundJobQueueState(IBackgroundJobClient client, IJobQueue queue, CancellationToken token)
    {
        string[] queues = null;
        await using (var connection = await client.OpenConnectionAsync(false, token).ConfigureAwait(false))
        {
            queues = await client.GetAllQueuesAsync(connection, null, token);
        }

        var processQueueStates = new Dictionary<string, Task<long>>();
        var cleanupQueueStates = new Dictionary<string, Task<long>>();
        foreach (var queueName in queues)
        {
            processQueueStates.Add(queueName, queue.GetBackgroundJobProcessQueueLengthAsync(queueName, token));
            cleanupQueueStates.Add(queueName, queue.GetBackgroundJobCleanupQueueLengthAsync(queueName, token));
        }

        var builder = new StringBuilder();
        builder.AppendLine("Pending background jobs:");
        foreach (var (queueName, countTask) in processQueueStates)
        {
            builder.AppendLine($"{queueName}: {await countTask}");
        }
        builder.AppendLine();
        builder.AppendLine("Pending cleanup background jobs:");
        foreach (var (queueName, countTask) in cleanupQueueStates)
        {
            builder.AppendLine($"{queueName}: {await countTask}");
        }

        return builder.ToString();
    }

    private static async Task<string> GetRecurringJobQueueState(IRecurringJobClient client, IJobQueue queue, CancellationToken token)
    {
        string[] queues = null;
        await using (var connection = await client.OpenConnectionAsync(false, token).ConfigureAwait(false))
        {
            queues = await client.GetAllQueuesAsync(connection, null, token);
        }

        var processQueueStates = new Dictionary<string, Task<long>>();
        foreach (var queueName in queues)
        {
            processQueueStates.Add(queueName, queue.GetRecurringJobProcessQueueLengthAsync(queueName, token));
        }

        var builder = new StringBuilder();
        builder.AppendLine("Pending recurring jobs:");
        foreach (var (queueName, countTask) in processQueueStates)
        {
            builder.AppendLine($"{queueName}: {await countTask}");
        }

        return builder.ToString();
    }

    private static string GetDaemonState(IDaemonExecutionContext context)
    {
        var builder = new StringBuilder();
        foreach (var daemon in context.Daemon.Colony.Daemons)
        {
            var state = daemon.State;
            builder.Append($"Daemon {daemon.Name} ({daemon.Status})");
            if (state != null)
            {
                builder.AppendLine(":");
                builder.AppendLine(state.ToString());
            }
            else
            {
                builder.AppendLine();
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }
}

public static class JobActions<T>
{
    public static async Task Save(IBackgroundJobExecutionContext context, T data, CancellationToken token = default)
    {
        if (await context.Job.TryGetDataAsync<T>("ProcessingState", token).ConfigureAwait(false) is (true, var savedData))
        {
            context.Log($"Data of type <{data?.GetType()}> was already saved to background job <{HiveLog.Job.IdParam}>. Value is <{savedData}>", context.Job.Id);
        }
        else
        {
            context.Log($"Saving data of type <{data?.GetType()}> to background job <{HiveLog.Job.IdParam}>", context.Job.Id);
            await context.Job.SetDataAsync("ProcessingState", data, token).ConfigureAwait(false);

            throw new Exception("Data was saved but oopsy job crashed");
        }
    }

    public static async Task Save(IBackgroundJobExecutionContext context, IEnumerable<T> data, CancellationToken token = default)
    {
        if (await context.Job.TryGetDataAsync<IEnumerable<T>>("ProcessingState", token).ConfigureAwait(false) is (true, var savedData))
        {
            context.Log($"Data of type <{data?.GetType()}> was already saved to background job <{HiveLog.Job.IdParam}>. Value is <{savedData}>", context.Job.Id);
        }
        else
        {
            context.Log($"Saving data of type <{data?.GetType()}> to background job <{HiveLog.Job.IdParam}>", context.Job.Id);
            await context.Job.SetDataAsync("ProcessingState", data, token).ConfigureAwait(false);

            throw new Exception("Data was saved but oopsy job crashed");
        }
    }
}

[Flags]
public enum SeedType
{
    Hello = 1,
    Data = 2,
    Plain = 4,
    LongRunning = 8
}