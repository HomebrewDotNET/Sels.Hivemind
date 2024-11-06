using Castle.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Fluent;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.HiveMind.Colony.Scheduler;
using Sels.HiveMind.Colony.Swarm.IdGenerators;
using Sels.HiveMind.DistributedLocking;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.ObjectValidationFramework.Extensions.Validation;
using Sels.ObjectValidationFramework.Profile;
using Sels.ObjectValidationFramework.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Contains the common configuration options for a swarm host.
    /// </summary>
    /// <typeparam name="TSelf">The top level type inheriting from this class</typeparam>
    /// <typeparam name="TOptions">The public readonly type of the current options</typeparam>
    public abstract class SwarmHostOptions<TSelf, TOptions> : ISwarmHostOptions<TOptions>
        where TSelf : TOptions
        where TOptions : ISwarmHostOptions<TOptions>
    {
        /// <summary>
        /// The unique name of this (sub) swarm.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// How many drones are assigned to this swarm.
        /// When set to null the default from <see cref="SwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public int? Drones { get; set; }
        /// <inheritdoc/>
        public string DroneAlias { get; set; } = "Drone";
        /// <inheritdoc/>
        [JsonIgnore]
        public Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>> DroneIdGeneratorFactory { get; set; }
        /// <summary>
        /// Sets this swarm as a dedicated swarm. Will only work on queues assigned to this swarm and not to queues assigned to any parent swarms.
        /// </summary>
        public bool IsDedicated { get; set; }
        /// <summary>
        /// If this swarm can also work on jobs in the <see cref="HiveMindyConstants.Queue.DefaultQueue"/> queue.
        /// When set to null default is true for the root swarm.
        /// </summary>
        public bool? WorkOnGlobalQueue { get; set; }
        /// <summary>
        /// The type of scheduler to use for the swarm. 
        /// When set to null the default from <see cref="SwarmHostDefaultOptions"/> will be used.
        /// </summary>
        public string SchedulerType { get; set; }
        /// <inheritdoc/>
        [JsonIgnore]
        public Func<IServiceProvider, JobSchedulerConfiguration, Task<IComponent<IJobScheduler>>> SchedulerFactory { get; set; }
        /// <summary>
        /// The name to assign to the created scheduler.
        /// The default is the name of the swarm.
        /// </summary>
        public string SchedulerName { get; set; }
        /// <summary>
        /// The queues that drones of this swarm can process jobs from.
        /// </summary>
        public List<SwarmQueue> Queues { get; set; }
        /// <inheritdoc/>
        IReadOnlyCollection<ISwarmQueue> ISwarmHostOptions<TOptions>.Queues => Queues;
        /// <summary>
        /// The options of any sub swarms.
        /// </summary>
        public List<TSelf> SubSwarmOptions { get; set; }
        /// <inheritdoc/>
        public TimeSpan? GracefulStoptime { get; set; }
        /// <inheritdoc/>
        public TimeSpan? MaxStoptime { get; set; }
        /// <inheritdoc/>
        IReadOnlyCollection<TOptions>? ISwarmHostOptions<TOptions>.SubSwarmOptions => SubSwarmOptions != null ? SubSwarmOptions.OfType<TOptions>().ToList() : null;
        /// <inheritdoc/>
        public TimeSpan? UnhandledExceptionSleepTime { get; set; }
        /// <inheritdoc/>
        public TimeSpan? LockExpirySafetyOffset { get; set; }
        /// <inheritdoc/>
        IReadOnlyList<ISwarmHostMiddlewareOptions<IJobSchedulerMiddleware>> ISwarmHostOptions<TOptions>.SchedulerMiddleware => SchedulerMiddleware;
        /// <inheritdoc/>
        public List<SwarmHostMiddlewareOptions<IJobSchedulerMiddleware>> SchedulerMiddleware { get; set; }
        /// <summary>
        /// The object to return for the fluent syntax.
        /// </summary>
        protected abstract TSelf Self { get; }


        /// <inheritdoc cref="SwarmHostOptions{TSelf, TOptions}"/>
        protected SwarmHostOptions()
        {
            _ = UseAlpabetIdGenerator();
        }

        /// <summary>
        /// Adds a new sub swarm with name <paramref name="name"/> configured using <paramref name="builder"/>.
        /// </summary>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="builder">Delegate to configure the created instance</param>
        /// <returns>Current options for method chaining</returns>
        public TSelf AddSubSwarm(string name, Action<TSelf> builder)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var options = CreateSubSwarmOptions(name, builder);
            SubSwarmOptions ??= new List<TSelf>();
            SubSwarmOptions.Add(options);

            return Self;
        }

        /// <summary>
        /// Adds a new queue that the current swarm can work on.
        /// </summary>
        /// <param name="name"><inheritdoc cref="SwarmQueue.Name"/></param>
        /// <param name="priority"><inheritdoc cref="SwarmQueue.Priority"/></param>
        /// <returns>Current options for method chaining</returns>
        public TSelf AddQueue(string name, byte? priority = null)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            Queues ??= new List<SwarmQueue>();
            Queues.Add(new SwarmQueue() { Name = name, Priority = priority });
            return Self;
        }



        /// <summary>
        /// Creates an new instance of <typeparamref name="TOptions"/> using <paramref name="name"/> and <paramref name="builder"/>.
        /// </summary>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="builder">Delegate to configure the created instance</param>
        /// <returns>The option instance configured from <paramref name="builder"/></returns>
        protected abstract TSelf CreateSubSwarmOptions(string name, Action<TSelf> builder);

        #region Scheduler
        /// <summary>
        /// Use a <see cref="PullthroughScheduler"/> for this swarm to schedule jobs.
        /// </summary>
        /// <param name="configure">Option delegate to configure the options</param>
        /// <returns>The current instance for method chaining</returns>
        public TSelf UsePullthroughScheduler(Action<PullthroughSchedulerOptions>? configure = null)
        {
            var options = new PullthroughSchedulerOptions();
            configure?.Invoke(options);

            SchedulerFactory = new Func<IServiceProvider, JobSchedulerConfiguration, Task<IComponent<IJobScheduler>>>((p, c) => new ScopedComponent<IJobScheduler>(PullthroughScheduler.SchedulerType, new PullthroughScheduler(options, c, p.GetRequiredService<ITaskManager>(), p.GetService<ILogger<PullthroughScheduler>>()), default).ToTaskResult<IComponent<IJobScheduler>>());
            return Self;
        }
        #endregion

        #region Id Generator
        /// <summary>
        /// Use <see cref="AlphabetIdGenerator"/> to generate drone ids.
        /// </summary>
        /// <param name="toLower">If the letter should be returned as lower case</param>
        /// <returns>Current options for method chaining</returns>
        public TSelf UseAlpabetIdGenerator(bool toLower = false)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Alphabet", new AlphabetIdGenerator(toLower), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        /// <summary>
        /// Use <see cref="NumericIdGenerator"/> to generate drone ids.
        /// </summary>
        /// <param name="padding">Optionally how many 0 should be added until we reach the configured lenght</param>
        /// <returns>Current options for method chaining</returns>
        public TSelf UseNumericIdGenerator(int? padding = null)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Numeric", new NumericIdGenerator(padding), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        /// <summary>
        /// Use <see cref="HexadecimalIdGenerator"/> to generate drone ids.
        /// </summary>
        /// <param name="padding">Optionally how many 0 should be added until we reach the configured lenght</param>
        /// <param name="toLower">If the letter should be returned as lower case</param>
        /// <returns>Current options for method chaining</returns>
        public TSelf UseHexadecimalIdGenerator(int? padding = null, bool toLower = false)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Hexadecimal", new HexadecimalIdGenerator(padding, toLower), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        /// <summary>
        /// Use <see cref="RomanIdGenerator"/> to generate drone ids.
        /// </summary>
        /// <param name="toLower">If the letter should be returned as lower case</param>
        /// <returns>Current options for method chaining</returns>
        public TSelf UseRomanIdGenerator(bool toLower = false)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Roman", new RomanIdGenerator(toLower), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        #endregion

        #region Middleware
        /// <summary>
        /// Adds a job scheduler middleware that will be used by the job scheduler of this swarm.
        /// </summary>
        /// <typeparam name="T">The type of the middleware to add</typeparam>
        /// <param name="factory">Delegate that will be used to to create the middleware</param>
        /// <param name="context">Optional context that can be used to provide input to the middleware</param>
        /// <param name="configureMiddleware">Optional delegate for configuring the middleware</param>
        /// <returns>Current options for method chaining</returns>
        public TSelf AddSchedulerMiddleware<T>(Func<IServiceProvider, Task<IComponent<T>>> factory, object? context = null, Action<SwarmHostMiddlewareConfigurationOptions>? configureMiddleware = null) where T : class, IJobSchedulerMiddleware
        {
            factory = Guard.IsNotNull(factory);

            var options = new SwarmHostMiddlewareOptions<IJobSchedulerMiddleware>()
            {
                Factory = async p => await factory(p).ConfigureAwait(false),
                Context = context
            };

            configureMiddleware?.Invoke(options.ConfigurationOptions);

            SchedulerMiddleware ??= new List<SwarmHostMiddlewareOptions<IJobSchedulerMiddleware>>();
            SchedulerMiddleware.Add(options);
            return Self;
        }

        #region DistributedLocking
        /// <summary>
        /// Adds a middleware that uses a distributed lock around dequeued jobs to provide synchonization across multiple colonies.
        /// Effectively adds a global processing limit that is shared by all colonies that use the same locking key.
        /// The default options limit the total amount of global concurrent executions per queue to 1.
        /// </summary>
        /// <param name="configure">Optional delegate that can be used to configure the input for the middleware</param>
        /// <param name="configureMiddleware">Optional delegate for configuring the middleware</param>
        /// <returns>Current options for method chaining></returns>
        public TSelf UseQueueDistributedLocking(Action<DistributedLockJobSchedulerMiddlewareOptions>? configure = null, Action<SwarmHostMiddlewareConfigurationOptions>? configureMiddleware = null)
        {
            var options = new DistributedLockJobSchedulerMiddlewareOptions();
            configure?.Invoke(options);

            return AddSchedulerMiddleware(p => Task.FromResult<IComponent<DistributedLockJobSchedulerMiddleware>>(
                new ScopedComponent<DistributedLockJobSchedulerMiddleware>(
                    nameof(DistributedLockJobSchedulerMiddleware), 
                    new DistributedLockJobSchedulerMiddleware(p.GetRequiredService<IDistributedLockServiceProvider>(), p.GetRequiredService<ITaskManager>(), p.GetService<ILogger<DistributedLockJobSchedulerMiddleware>>())
                    , null, true)
                ), options, configureMiddleware);
        }
        #endregion
        #endregion
    }

    /// <inheritdoc cref="ISwarmQueue"/>
    public class SwarmQueue : ISwarmQueue
    {
        /// <inheritdoc/>
        public string Name { get; set; }
        /// <inheritdoc/>
        public byte? Priority { get; set; }
    }

    /// <summary>
    /// Contains the validation rules for <see cref="SwarmHostOptions{TSelf, TOptions}"/>.
    /// </summary>
    public class SwarmHostOptionsValidationProfile<TSelf, TOptions> : ValidationProfile<string>
        where TSelf : TOptions
        where TOptions : ISwarmHostOptions<TOptions>
    {
        /// <inheritdoc cref="SwarmHostOptionsValidationProfile{TSelf, TOptions}"/>
        public SwarmHostOptionsValidationProfile()
        {
            CreateValidationFor<ISwarmHostOptions<TOptions>>()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Drones)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Value >= 0, x => $"Must be larger or equal to 0 when not set to null")
                .ForProperty(x => x.DroneAlias)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.DroneIdGeneratorFactory)
                    .CannotBeNull()
                .ForProperty(x => x.MaxStoptime)
                    .NextWhen(x => x.Value.HasValue && x.Source.GracefulStoptime.HasValue)
                    .ValidIf(x => x.Value > x.Source.GracefulStoptime, x => $"Must be larger than <{nameof(x.Source.GracefulStoptime)}>")
                .ForElements(x => x.SubSwarmOptions)
                    .CannotBeNull()
                // Only for root options
                .ValidateNextWhen(x => x.CurrentParent == null)
                .ForSource()
                    .InvalidIf(x =>
                    {
                        var allNames = x.Source.GetDefinedNames();

                        var grouped = allNames.GroupAsDictionary(x => x, StringComparer.OrdinalIgnoreCase);
                        var duplicates = grouped.Where(x => x.Value.Count > 1).Select(x => x.Key).Distinct().ToArray();
                        x.ValidatorResult = duplicates;
                        return duplicates.HasValue();
                    }, x => $"Duplicate swarm names used. The following names are used by 2 or more swarms <{x.ValidatorResult.CastTo<string[]>().JoinString(", ")}>");

            CreateValidationFor<ISwarmQueue>()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace();
        }
    }
}
