using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Sels.Core;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Text;
using Sels.HiveMind;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using System.ServiceModel.Channels;
using System.Xml.Schema;
using static Sels.HiveMind.HiveMindConstants;

await Helper.Console.RunAsync(Actions.QueryJobAsync);


public static class Actions
{
    public static async Task CreateJobAsync()
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.None);
                                x.AddFilter("Sels.HiveMind", LogLevel.Error);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        string id = null;
        foreach(var i in Enumerable.Range(0, 100))
        {
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Created job <{id}> in <{x.PrintTotalMs()}>")))
            {
                var message = $"Hello from iteration {i}";
                id = await client.CreateAsync(() => Hello(message), x => x.WithProperty("Text", "Hello!")
                                                                          .WithProperty("Number", 1998)
                                                                          .WithProperty("Digit", 12.90)
                                                                          .WithProperty("Date", DateTime.Now)
                                                                          .WithProperty("Other", Array.Empty<string>())
                                             , token: Helper.App.ApplicationToken);
            }
            IBackgroundJobState[]? states = null;
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Fetched job <{id}> with state history <{states?.Select(x => x.Name).JoinString("=>")}> in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetAsync(id, token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                //Console.WriteLine($"\tJob properties:");
                //job.Properties.Execute(x => Console.WriteLine($"\t\t{x.Key}: {x.Value}"));
                await job.DisposeAsync();
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Fetched job <{id}> with state history <{states.Select(x => x.Name).JoinString("=>")}> with write lock in <{x.PrintTotalMs()}>")))
            {
                await using(var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken))
                {
                    states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                    //Console.WriteLine($"\tJob properties:");
                    //job.Properties.Execute(x => Console.WriteLine($"\t\t{x.Key}: {x.Value}"));
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Updated job <{id}> in <{x.PrintTotalMs()}>")))
            {
                await using (var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken))
                {
                    await job.ChangeStateAsync(new FailedState(new Exception("Some error")), Helper.App.ApplicationToken);
                    job.SetProperty("Text", "Hello again!");
                    job.SetProperty("Number", 1997);
                    job.SetProperty("Digit", 666.666);
                    job.SetProperty("Date", DateTime.Now);
                    job.RemoveProperty("Other");
                    await job.ChangeStateAsync(new DeletedState() { Reason = "Job not needed anymore"}, Helper.App.ApplicationToken);
                    await job.SaveChangesAsync(Helper.App.ApplicationToken);
                };
            }
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Fetched job <{id}> with state history <{states?.Select(x => x.Name).JoinString("=>")}> in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetAsync(id, token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                Console.WriteLine($"\tJob properties:");
                job.Properties.Execute(x => Console.WriteLine($"\t\t{x.Key}: {x.Value}"));
                await job.DisposeAsync();
            }

            Console.WriteLine();
        }
    }

    public static async Task QueryJobAsync()
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddHiveMindMySqlQueue()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.Error);
                                x.AddFilter("Sels.Core.Async.Queue", LogLevel.Warning);
                                x.AddFilter("Sels.Core.Async.TaskManagement", LogLevel.Warning);
                                x.AddFilter("Sels.HiveMind", LogLevel.Warning);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        var logger = provider.GetRequiredService<ILogger<Program>>();

        foreach (var i in Enumerable.Range(0, 100))
        {
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Created batch {i+1} in <{x.PrintTotalMs()}>")))
            {
                await using (var clientConnection = await client.OpenConnectionAsync("Main", true, Helper.App.ApplicationToken))
                {
                    await client.CreateAsync(clientConnection, () => Hello($"Hello from iteration {i}"), x => x.InState(new IdleState()), Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello($"Hello from iteration {i}"), token: Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello($"Hello from iteration {i}"), x => x.InState(new FailedState(new SystemException("Something went wrong"))), Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello($"Hello from iteration {i}"), x => x.WithProperty("Index", i), Helper.App.ApplicationToken);
                    await client.CreateAsync(clientConnection, () => Hello($"Hello from iteration {i}"), x => x.InQueue("Testing", QueuePriority.Critical), Helper.App.ApplicationToken);

                    await clientConnection.CommitAsync(Helper.App.ApplicationToken);
                }
            }
        }

        // Dequeue
        foreach (var i in Enumerable.Range(0, 10))
        {
            int dequeued = 0;
            long total = 0;

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Dequeued <{dequeued}> failed jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.DequeueAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName), 100, "Jens", QueryBackgroundJobOrderByTarget.ModifiedAt, true, Helper.App.ApplicationToken))
                {
                    dequeued = result.Results.Length;
                    total = result.Total;
                }
            }
            Console.WriteLine();
        }
        // Query
        foreach (var i in Enumerable.Range(0, 10))
        {
            int queried = 0;
            long total = 0;

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> deleted jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(DeletedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> idle jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(IdleState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> enqueued jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> failed jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName).And.CurrentState.Property<FailedState>(x => x.Message).Like("Something*"), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs with index property out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.Property("Index").AsInt.Not.EqualTo(null), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs with critical priority out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.Priority.EqualTo(QueuePriority.Critical), 10, 1, QueryBackgroundJobOrderByTarget.Priority, false, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs in test queue out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.Queue.EqualTo("Testing"), 10, 1, QueryBackgroundJobOrderByTarget.Priority, false, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Length;
                    total = result.Total;
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
                total = await client.QueryCountAsync(x => x.CurrentState.Name.EqualTo(DeletedState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> idle jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.CurrentState.Name.EqualTo(IdleState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> enqueued jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> enqueued jobs that can be processed in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName).And.Group(x => x.CurrentState.Property<EnqueuedState>(x => x.DelayedToUtc).EqualTo(null).Or.CurrentState.Property<EnqueuedState>(x => x.DelayedToUtc).LesserOrEqualTo(DateTime.UtcNow)), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> failed jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs that where retried in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.Property(HiveMindConstants.Job.Properties.RetryCount).AsInt.GreaterOrEqualTo(1), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs with no retries in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.Property(HiveMindConstants.Job.Properties.RetryCount).AsInt.EqualTo(0).Or
                                                           .Property(HiveMindConstants.Job.Properties.RetryCount).AsInt.EqualTo(null), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> total jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(token: Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs in Global queue in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.Queue.EqualTo(HiveMindConstants.Queue.DefaultQueue), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> jobs in Testing queue in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.Queue.EqualTo("Testing"), Helper.App.ApplicationToken);
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{total}> locked jobs in <{x.PrintTotalMs()}>")))
            {
                total = await client.QueryCountAsync(x => x.LockedBy.Not.EqualTo(null), Helper.App.ApplicationToken);
            }
            Console.WriteLine();
        }
    }

    public static int Hello(string message)
    {
        Console.WriteLine(message);
        return Helper.Random.GetRandomInt(0, 10000);
    }
}