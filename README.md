# Hivemind
Hivemind is a framework that provides you with all the building blocks you need to setup your asynchronous background processing backend. 
It is a modular, persistant and distributed job scheduler that provides you background jobs, recurring jobs, daemons, worker swarms with advanced configuration and many more features.
A big focus of the framework is flexability, performance, reliability and most importantly the Developer Experience as coding should be fun.

## Features
Here's a small overview of what HiveMind has to offer.

### Background job creation
HiveMind does not require you to implement any interfaces or extend any classes, and rather let's you use expressions to define how to execute your jobs.  
It allows you to either call static methods or methods on the instance of a certain type.  
Any parameter can be passed to the methods as long as it can be serialized.  
Job instance activation also works out of the box with any IoC container that properly supports the ```IServiceProvider``` interface.  

```csharp
var provider = new ServiceCollection()
                   .AddHiveMind()
                   .AddHiveMindMySqlQueue()
                   .AddHiveMindMySqlStorage()
                   .BuildServiceProvider();    

var client = provider.GetRequiredService<IBackgroundJobClient>();

var jobId = await client.CreateAsync<ConsoleWriterJob>(x => x.Run(default(IBackgroundJobExecutionContext), "I'm a job argument!", default(CancellationToken)), token: token);  
_ = await client.CreateAsync<ConsoleWriterJob>(x => x.RunAsync(default(IBackgroundJobExecutionContext), "I'm a job argument to an async job that returns a value!", default(CancellationToken)), token: token);
_ = await client.CreateAsync(() => HelloWorld("I'm a job parameter to a static method!"), token: token);
```

### Background job queues and priorities
When scheduling background jobs they are assigned a queue and a priority.   
Queues allow you to group jobs together while the priority allows you you prioritise some jobs over others within the same queue.  
An unlimited number of queues can be defined and 5 priorities can be assigned to jobs.

```csharp
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
```	

### Delayed background jobs
It is also possible to delay the execution of a background job by specifying the date after which the job is allowed to be executed.

```csharp
var provider = new ServiceCollection()
                   .AddHiveMind()
                   .AddHiveMindMySqlQueue()
                   .AddHiveMindMySqlStorage()
                   .BuildServiceProvider();

var client = provider.GetRequiredService<IBackgroundJobClient>();

_ = await client.CreateAsync(() => DoStuff(), x => x.DelayExecutionTo(DateTime.UtcNow.AddHours(8)), token: token);
_ = await client.CreateAsync(() => DoStuff(), x => x.DelayExecutionTo(DateTime.UtcNow.AddMinutes(5)), token: token);
_ = await client.CreateAsync(() => DoStuff(), x => x.DelayExecutionTo(DateTime.UtcNow.AddSeconds(1)), token: token);
```

### Background job continuations
Background jobs can also be chained after each other by specifying a parent job to wait on.  
When the parent job transitions into a state of choice, the awaiting background job will be enqueued.

```csharp
var provider = new ServiceCollection()
                   .AddHiveMind()
                   .AddHiveMindMySqlQueue()
                   .AddHiveMindMySqlStorage()
                   .BuildServiceProvider();

var client = provider.GetRequiredService<IBackgroundJobClient>();

await using (var connection = await client.OpenConnectionAsync(true, token))
{
    var firstJobId = await client.CreateAsync(() => DoStuff(), token: token);
    var secondJobId = await client.CreateAsync(() => DoStuff(), x => x.EnqueueAfter(firstJobId, BackgroundJobContinuationStates.Succeeded), token: token);
    var thirdJobId = await client.CreateAsync(() => DoStuff(), x => x.EnqueueAfter(secondJobId, BackgroundJobContinuationStates.Any), token: token);
    _ = await client.CreateAsync(() => DoStuff(), x => x.EnqueueAfter(thirdJobId, BackgroundJobContinuationStates.Finished), token: token);

    await connection.CommitAsync(token);
}
```	

### Background job execution middleware
Like the ASP.NET Core middleware pipeline, HiveMind also provides a pipeline based on middleware that sits in between the Drone that executes the job and the job invocation itself.
This allows you to execute code before and/or after the background job is executed, overwriting the result of the background job, skip the execution in it's entirety, ...
Middleware can both be assigned to the jobs themselves or Swarms of Drones. (Seen later)

```csharp
/// <summary>
/// Example middleware.
/// </summary>
public class BackgroundJobExampleMiddleware : IBackgroundJobMiddleware
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(IBackgroundJobExecutionContext jobContext, object context, Func<IBackgroundJobExecutionContext, CancellationToken, Task> next, CancellationToken token)
    {
        var arguments = jobContext.InvocationArguments; // Get arguments for the job method
        var job = jobContext.Job; // Access writeable job

        string input = context as string; // Custom input for middleware

        // Do stuff before the job is executed

        await next(jobContext, token); // Call next middleare or invoke the job

        // Do stuff after the job is executed

        var result = jobContext.Result; // Get the result of the job. Either object returned by method or exception
    }
}
```
```csharp
 var provider = new ServiceCollection()
                    .AddHiveMind()
                    .AddHiveMindMySqlQueue()
                    .AddHiveMindMySqlStorage()
                    .BuildServiceProvider();

 var client = provider.GetRequiredService<IBackgroundJobClient>();

 _ = await client.CreateAsync(() => DoStuff(), x => x.WithMiddleWare<BackgroundJobExampleMiddleware>("I'm input for the middleware", priority: 12), token: token);
```

### Query Api
Next to basic creation and fetching of background jobs, HiveMind also provides a query api that allows you to search, count and lock background jobs.
It's possible to query on typed properties assigned to jobs, their state, the properties of the states, the queue they are in, ...
Also supports sorting and pagination.

```csharp   
var provider = new ServiceCollection()
                   .AddHiveMind()
                   .AddHiveMindMySqlQueue()
                   .AddHiveMindMySqlStorage()
                   .BuildServiceProvider();

var client = provider.GetRequiredService<IBackgroundJobClient>();
var tenantId = Guid.NewGuid();

_ = await client.CreateAsync(() => DoStuff(), x => x.WithProperty(nameof(tenantId), tenantId), token: token); // Create job for the tenant
var amountOfJobsForTenant = await client.CountAsync(x => x.Property(nameof(tenantId)).AsGuid.EqualTo(tenantId)); // Get amount of jobs created for the tenant
await using var tenantJobs = await client.SearchAsync(x => x.Property(nameof(tenantId)).AsGuid.EqualTo(tenantId).And.Priority.EqualTo(QueuePriority.Critical), 
                                                      pageSize: 100, 
                                                      page: 1, 
                                                      orderBy: QueryBackgroundJobOrderByTarget.CreatedAt, 
                                                      orderByDescending: true, 
                                                      token: token); // Search for first 100 jobs created for the tenant of a certain priority
```

### Background jobs as state machines
Background jobs are implemented as state machines and can be transitioned between states.
Out of the box HiveMind adds states and handlers to drive the basic processing flow of background jobs but can easily be extended with custom states and handlers. 

```csharp
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
```

### Implementing background jobs
As said previously background can be invoked using any custom made method.  
There are some special types that HiveMind will take into account when invoking a job.  
The first is the `CancellationToken` type that will be replaced. This token will be cancelled when the job is requested to stop executing.  
The second is the `IBackgroundJobExecutionContext` interface that will be replaced with an instance of the current execution context.  
This context contains things like the background job itself with all it's state, who is executing the job, ...  
It also allows you to create log messages tied to the background job.

```csharp
/// <summary>
/// Example background job implementation.
/// </summary>
public class BackgroundJobExample
{
    /// <summary>
    /// Example method that can be invoked by a Drone.
    /// </summary>
    /// <param name="context">HiveMind execution context</param>
    /// <param name="input">Custom input to the job</param>
    /// <param name="token">Token that will be cancelled to cancel the job</param>
    /// <returns>Custom processing result</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<object> ExecuteAsync(IBackgroundJobExecutionContext context, BackgroundJobExampleInput input, CancellationToken token)
    {
        // Access background job state
        if(!context.Job.TryGetProperty<Guid>("TenantId", out var tenantId))
        {
            context.Log(LogLevel.Warning, $"Tenant id is missing, can't execute job");
            throw new InvalidOperationException("Expected tenant id to be assigned to job but was missing");
        }

        // Get input to job
        var entityId = input.EntityId;

        // Check if already processed
        if(await context.Job.TryGetDataAsync<bool>("EntityProcessed", token) is (true, true))
        {
            context.Log(LogLevel.Warning, $"Entity {entityId} was already processed. Skipping");
        }
        else
        {
            // Process entity
            context.Log($"Processing entity {entityId}");

            // Save processing state
            await context.Job.SetDataAsync("EntityProcessed", true, token);
        }

        return $"Processed entity {entityId}";
    }
}

/// <summary>
/// Example custom input for background job.
/// </summary>
public class BackgroundJobExampleInput
{
    public string EntityId { get; set; }
}
```
```csharp
var provider = new ServiceCollection()
                   .AddHiveMind()
                   .AddHiveMindMySqlQueue()
                   .AddHiveMindMySqlStorage()
                   .BuildServiceProvider();

var client = provider.GetRequiredService<IBackgroundJobClient>();

_ = await client.CreateAsync<BackgroundJobExample>(x => x.ExecuteAsync(default(IBackgroundJobExecutionContext), new BackgroundJobExampleInput() { EntityId = Guid.NewGuid().ToString() }, default(CancellationToken)), token: token);
```

### Environments
HiveMind environments allows you setup self contained envionments that either use the same or different storages and queue providers or use the same resources but with a boundary.  
This gives you the flexibility to setup your projects in a way that suits your needs.

```csharp
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
```

### Transactions
It is possible to start transactions for all actions that interact with a HiveMind storage.
This allows you to execute multiple actions in an atomic way.

```csharp 
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
```

### Mediator pattern
HiveMind makes use of the Mediator pattern to easily allow you to extend the framework with your own features.
Various components dispatch events (broadcasted to all listeners) and requests (broadcasted to all listeners until one returns a result) that you can subscribe to.

```csharp
/// <summary>
/// Request handlers that cancels the exection of a job if the tenant has not paid their bill.
/// </summary>
public class TenantPaymentEnforcer : IBackgroundJobStateElectionRequestHandler
{
    /// <inheritdoc/>
    public byte? Priority => 0; // Always run first

    /// <inheritdoc/>
    public Task<RequestResponse<IBackgroundJobState>> TryRespondAsync(IRequestHandlerContext context, BackgroundJobStateElectionRequest request, CancellationToken token)
    {
        if (request.ElectedState is ExecutingState && request.Job.TryGetProperty<Guid>("TenantId", out var tenantId))
        {
            // Check if tenant has paid their bill
            if (!HasPaidBill(tenantId))
            {
                return Task.FromResult(RequestResponse<IBackgroundJobState>.Success(new FailedState($"Tenant <{tenantId}> has not paid it's bills so not allowing execution")));
            }
        }

        return Task.FromResult(RequestResponse<IBackgroundJobState>.Reject());
    }

    private bool HasPaidBill(Guid tenantId)
    {
        // Check if tenant has paid their bill
        return false;
    }
}
```
```csharp
var services = new ServiceCollection()
                   .AddHiveMind()
                   .AddHiveMindMySqlQueue()
                   .AddHiveMindMySqlStorage();

services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, TenantPaymentEnforcer>(x => x.AsScoped().WithBehaviour(RegisterBehaviour.TryAddImplementation));
```

### Task Parallell Library
HiveMind does not manage it's own threads and rather relies on the powerfull [Task Parallell Library](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) to manage asynchronous execution.
This allows it to make efficient use of threads when executing the various features of the framework.

### Microsoft api integration
The framework also integrates easily with some of the most used Microsoft apis such as `IServiceCollection`, `IServiceProvider`, `ILogger`, `IOptions<T>`, `IConfiguration`, ...	
This makes it easy to integrate with other packages.

```csharp	
 var services = new ServiceCollection()
                    .AddHiveMind()
                    .AddHiveMindMySqlQueue(x => x.ForEnvironment("Custom"))
                    .AddHiveMindMySqlStorage(x => x.ForEnvironment("Custom")); // Register services using IServiceCollection

 services.AddLogging(x => x.AddConsole()); // Native support for ILogger
 services.Configure<HiveMindOptions>("Custom", x => x.CompletedBackgroundJobRetention = TimeSpan.FromDays(1)); // Configure options per environment using IOptions<T> framework. Also supports appsettings configuration.
 services.AddAutofac(); // Native support for IServiceProvider
```