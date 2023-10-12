using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Sels.Core;
using Sels.Core.Extensions.DateTimes;
using Sels.HiveMind.Client;

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
                                x.AddFilter("Sels.HiveMind", LogLevel.Information);
                            })
                            .BuildServiceProvider();

        var client = provider.GetRequiredService<IBackgroundJobClient>();
        string id = null;
        foreach(var i in Enumerable.Range(0, 10))
        {
            using (Helper.Time.CaptureDuration(x => Console.WriteLine($"Created job <{id}> in <{x.PrintTotalMs()}>")))
            {
                var message = $"Hello from iteration {i}";
                id = await client.CreateAsync(() => Hello(message));
            }
        }
    }


    private static int Hello(string message)
    {
        Console.WriteLine(message);
        return Helper.Random.GetRandomInt(0, 10000);
    }
}