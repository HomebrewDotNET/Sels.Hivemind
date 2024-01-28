// See https://aka.ms/new-console-template for more information
using Sels.Core;
using Sels.HiveMind.Examples;

await Helper.Console.RunAsync(async () =>
{
    await BackgroundJobExamples.RunAllAsync(default);
    await CoreExamples.RunAllAsync(default);
});