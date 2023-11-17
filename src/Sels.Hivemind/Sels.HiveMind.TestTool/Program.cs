using Castle.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Logging;
using Sels.Core.Extensions.Text;
using Sels.HiveMind;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System.ServiceModel.Channels;
using System.Xml.Schema;
using static Sels.HiveMind.HiveMindConstants;

await Helper.Console.RunAsync(() => Actions.DequeueJobs(1, 1));


public static class Actions
{
    public static async Task CreateJobsAsync()
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

        // Create awaiting
        foreach (var i in Enumerable.Range(0, 10))
        {
            string jobId = null;
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Created job <{jobId}> with 5 awaiting jobs in <{x.PrintTotalMs()}>")))
            {
                await using (var connection = await client.OpenConnectionAsync(true, Helper.App.ApplicationToken))
                {
                    jobId = await client.CreateAsync(() => Hello($"Hello from iteration {i}"));
                    _ = await client.CreateAsync(() => Hello($"Awaiting 1: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Succeeded | BackgroundJobContinuationStates.Failed));
                    _ = await client.CreateAsync(() => Hello($"Awaiting 2: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Any));
                    _ = await client.CreateAsync(() => Hello($"Awaiting 3: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, null));
                    _ = await client.CreateAsync(() => Hello($"Awaiting 4: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, true, DeletedState.StateName));
                    _ = await client.CreateAsync(() => Hello($"Awaiting 5: Hello from iteration {i}"), x => x.EnqueueAfter(jobId, true, IdleState.StateName));
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Triggered 5 awaiting jobs for job <{jobId}> in <{x.PrintTotalMs()}>")))
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
                await using var clientConnection = await client.OpenConnectionAsync("Main", true, Helper.App.ApplicationToken);
                var result = await client.DequeueAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName), 10, "Jens", false, QueryBackgroundJobOrderByTarget.ModifiedAt, true, Helper.App.ApplicationToken);
                dequeued = result.Results.Count;
                total = result.Total;
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
            long total = 0;

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> deleted jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(DeletedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> idle jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(IdleState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> enqueued jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(EnqueuedState.StateName), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> failed jobs out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.CurrentState.Name.EqualTo(FailedState.StateName).And.CurrentState.Property<FailedState>(x => x.Message).Like("Something*"), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs with index property out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.Property("Index").AsInt.Not.EqualTo(null), 10, 1, QueryBackgroundJobOrderByTarget.ModifiedAt, true, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs with critical priority out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.Priority.EqualTo(QueuePriority.Critical), 10, 1, QueryBackgroundJobOrderByTarget.Priority, false, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
                    total = result.Total;
                }
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Queried <{queried}> jobs in test queue out of the total <{total}> in <{x.PrintTotalMs()}>")))
            {
                await using (var result = await client.QueryAsync(x => x.Queue.EqualTo("Testing"), 10, 1, QueryBackgroundJobOrderByTarget.Priority, false, token: Helper.App.ApplicationToken))
                {
                    queried = result.Results.Count;
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
                                    var jobId = await client.CreateAsync(connection, () => Hello($"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello($"Hello from {Environment.ProcessId} at <{DateTime.Now}> awaiting {jobId}"), x => x.EnqueueAfter(jobId, BackgroundJobContinuationStates.Any), t);
                                    _ = await client.CreateAsync(connection, () => Hello($"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("01.Process", allPriorities.GetRandomItem()), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello($"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("02.Process", allPriorities.GetRandomItem()), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello($"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("03.Process", allPriorities.GetRandomItem()), token: t);
                                    _ = await client.CreateAsync(connection, () => Hello($"Hello from {Environment.ProcessId} at <{DateTime.Now}>"), x => x.InQueue("04.Process", allPriorities.GetRandomItem()), token: t);
                                    currentSize++;
                                }

                                await connection.CommitAsync(t);

                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch(Exception ex)
                            {
                                logger.Log($"{workerId} ran into issue", ex);
                            }
                        }
                    }
                }

                logger.Log($"Worker <{x}> stopping");
            }, x => x.WithCreationOptions(TaskCreationOptions.LongRunning).WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepAlive)); 
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
                await using var queueScope = await queueProvider.GetQueueAsync(HiveMindConstants.DefaultEnvironmentName, Helper.App.ApplicationToken);
                var queue = queueScope.Component;
                logger.Log($"Worker <{x}> starting");
                while (!t.IsCancellationRequested)
                {
                    try
                    {
                        using (Helper.Time.CaptureDuration(x => logger.Log($"Worker <{workerId}> dequeued <{dequeueSize}> jobs from queues <{workerQueues.JoinString('|')}> in <{x.PrintTotalMs()}>")))
                        {
                            foreach (var dequeued in await queue.DequeueAsync(HiveMindConstants.Queue.BackgroundJobProcessQueueType, workerQueues, dequeueSize, Helper.App.ApplicationToken))
                            {
                                logger.Log($"Dequeued job <{dequeued.JobId}> with priority <{dequeued.Priority}> from queue <{dequeued.Queue}> enqueued at <{dequeued.EnqueuedAtUtc}>");
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
            }, x => x.WithCreationOptions(TaskCreationOptions.LongRunning).WithManagedOptions(ManagedTaskOptions.GracefulCancellation | ManagedTaskOptions.KeepAlive));
        });

        await Helper.Async.WaitUntilCancellation(Helper.App.ApplicationToken);

        await taskManager.StopAllForAsync(queueProvider);
    }

    public static int Hello(string message)
    {
        Console.WriteLine(message);
        return Helper.Random.GetRandomInt(0, 10000);
    }
}