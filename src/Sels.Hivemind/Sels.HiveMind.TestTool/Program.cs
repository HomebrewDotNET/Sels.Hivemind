using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Sels.Core;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using static Sels.HiveMind.HiveMindConstants;

await Helper.Console.RunAsync(Actions.CreateJobAsync);


public static class Actions
{
    public static async Task CreateJobAsync()
    {
        var provider = new ServiceCollection()
                            .AddHiveMind()
                            .AddHiveMindMySqlStorage()
                            .AddLogging(x =>
                            {
                                x.AddConsole();
                                x.SetMinimumLevel(LogLevel.None);
                                x.AddFilter("Sels.HiveMind", LogLevel.Warning);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        string id = null;
        foreach(var i in Enumerable.Range(0, 10))
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


    public static int Hello(string message)
    {
        Console.WriteLine(message);
        return Helper.Random.GetRandomInt(0, 10000);
    }
}