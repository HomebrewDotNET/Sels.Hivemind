using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Sels.Core;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;

await Helper.Console.RunAsync(Actions.CreateJobAsync);


internal static class Actions
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
        foreach(var i in Enumerable.Range(0, 3))
        {
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Created job <{id}> in <{x.PrintTotalMs()}>")))
            {
                var message = $"Hello from iteration {i}";
                id = await client.CreateAsync(() => Hello(message), token: Helper.App.ApplicationToken);
            }
            IBackgroundJobState[]? states = null;
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Fetched job <{id}> with state history <{states?.Select(x => x.Name).JoinString("=>")}> in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetAsync(id, token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                await job.DisposeAsync();
            }

            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Fetched job <{id}> with state history <{states.Select(x => x.Name).JoinString("=>")}> with write lock in <{x.PrintTotalMs()}>")))
            {
                var job = await client.GetWithLockAsync(id, "Jens", token: Helper.App.ApplicationToken);
                states = Helper.Collection.EnumerateAll(job.StateHistory, job.State.AsEnumerable()).ToArray();
                await job.DisposeAsync();
            }
        }
    }


    private static int Hello(string message)
    {
        Console.WriteLine(message);
        return Helper.Random.GetRandomInt(0, 10000);
    }
}