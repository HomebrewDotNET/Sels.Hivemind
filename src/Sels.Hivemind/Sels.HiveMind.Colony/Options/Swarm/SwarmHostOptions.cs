using Castle.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Fluent;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Threading;
using Sels.HiveMind.Colony.Swarm.IdGenerators;
using Sels.HiveMind.Scheduler;
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
    /// <typeparam name="TOptions">The type of inhereting this class</typeparam>
    public abstract class SwarmHostOptions<TOptions> : ISwarmHostOptions<TOptions>
        where TOptions : SwarmHostOptions<TOptions>
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
        public List<TOptions> SubSwarmOptions { get; set; }
        /// <inheritdoc/>
        public TimeSpan? GracefulStoptime { get; set; }
        /// <inheritdoc/>
        public TimeSpan? MaxStoptime { get; set; }
        /// <inheritdoc/>
        IReadOnlyCollection<TOptions> ISwarmHostOptions<TOptions>.SubSwarmOptions => SubSwarmOptions;
        /// <inheritdoc/>
        public TimeSpan? UnhandledExceptionSleepTime { get; set; }
        /// <inheritdoc/>
        public TimeSpan? LockExpirySafetyOffset { get; set; }

        /// <summary>
        /// The object to return for the fluent syntax.
        /// </summary>
        protected abstract TOptions Self { get; }

        /// <inheritdoc cref="SwarmHostOptions{TOptions}"/>
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
        public TOptions AddSubSwarm(string name, Action<TOptions> builder)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            var options = CreateSubSwarmOptions(name, builder);
            SubSwarmOptions ??= new List<TOptions>();
            SubSwarmOptions.Add(options);

            return Self;
        }

        /// <summary>
        /// Adds a new queue that the current swarm can work on.
        /// </summary>
        /// <param name="name"><inheritdoc cref="SwarmQueue.Name"/></param>
        /// <param name="priority"><inheritdoc cref="SwarmQueue.Priority"/></param>
        /// <returns>Current options for method chaining</returns>
        public TOptions AddQueue(string name, byte? priority = null)
        {
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));

            Queues ??= new List<SwarmQueue>();
            Queues.Add(new SwarmQueue() { Name = name, Priority = priority });
            return Self;
        }

        /// <summary>
        /// Gets the name of the swarm and any sub swarms with null, empty and whitespace filtered out.
        /// </summary>
        /// <returns>The names of the swarm and any sub swarms with null, empty and whitespace filtered out</returns>
        public IEnumerable<string> GetDefinedNames()
        {
            if (Name.HasValue()) yield return Name;

            if (SubSwarmOptions.HasValue())
            {
                foreach(var subSwarmOption in SubSwarmOptions.Where(x => x != null))
                {
                    foreach(var subName in subSwarmOption.GetDefinedNames())
                    {
                        yield return subName;
                    }
                }
            }
        }

        /// <summary>
        /// Creates an new instance of <typeparamref name="TOptions"/> using <paramref name="name"/> and <paramref name="builder"/>.
        /// </summary>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="builder">Delegate to configure the created instance</param>
        /// <returns>The option instance configured from <paramref name="builder"/></returns>
        protected abstract TOptions CreateSubSwarmOptions(string name, Action<TOptions> builder);

        #region Scheduler
        /// <summary>
        /// Use a <see cref="PullthroughScheduler"/> for this swarm to schedule jobs.
        /// </summary>
        /// <param name="configure">Option delegate to configure the options</param>
        /// <returns>The current instance for method chaining</returns>
        public TOptions UsePullthroughScheduler(Action<PullthroughSchedulerOptions> configure = null)
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
        public TOptions UseAlpabetIdGenerator(bool toLower = false)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Alphabet", new AlphabetIdGenerator(toLower), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        /// <summary>
        /// Use <see cref="NumericIdGenerator"/> to generate drone ids.
        /// </summary>
        /// <param name="padding">Optionally how many 0 should be added until we reach the configured lenght</param>
        /// <returns>Current options for method chaining</returns>
        public TOptions UseNumericIdGenerator(int? padding = null)
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
        public TOptions UseHexadecimalIdGenerator(int? padding = null, bool toLower = false)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Hexadecimal", new HexadecimalIdGenerator(padding, toLower), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        /// <summary>
        /// Use <see cref="RomanIdGenerator"/> to generate drone ids.
        /// </summary>
        /// <param name="toLower">If the letter should be returned as lower case</param>
        /// <returns>Current options for method chaining</returns>
        public TOptions UseRomanIdGenerator(bool toLower = false)
        {
            DroneIdGeneratorFactory = new Func<IServiceProvider, Task<IComponent<IDroneIdGenerator>>>(p => new ScopedComponent<IDroneIdGenerator>("Roman", new RomanIdGenerator(toLower), default).ToTaskResult<IComponent<IDroneIdGenerator>>());
            return Self;
        }
        #endregion
    }

    /// <summary>
    /// Contains the common configuration options for a swarm host.
    /// </summary>
    public class SwarmHostOptions : SwarmHostOptions<SwarmHostOptions>
    {
        /// <inheritdoc/>
        protected override SwarmHostOptions Self => this;

        /// <inheritdoc cref="SwarmHostOptions"/>
        public SwarmHostOptions() : base()
        {

        }

        /// <inheritdoc cref="SwarmHostOptions"/>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="configurator">Delegate to configure this instance</param>
        public SwarmHostOptions(string name, Action<SwarmHostOptions> configurator)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            configurator.ValidateArgument(nameof(configurator))(this);
        }

        /// <inheritdoc/>
        protected override SwarmHostOptions CreateSubSwarmOptions(string name, Action<SwarmHostOptions> builder)
        {
            return new SwarmHostOptions(name, builder);
        }
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
    /// Contains the validation rules for <see cref="SwarmHostOptions{TOptions}"/>.
    /// </summary>
    public class SwarmHostOptionsValidationProfile<TOptions> : ValidationProfile<string>
        where TOptions : SwarmHostOptions<TOptions>
    {
        /// <inheritdoc cref="SwarmHostOptionsValidationProfile{TOptions}"/>
        public SwarmHostOptionsValidationProfile()
        {
            CreateValidationFor<SwarmHostOptions<TOptions>>()
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

    /// <summary>
    /// Contains the validation rules for <see cref="SwarmHostOptions"/>.
    /// </summary>
    public class SwarmHostOptionsValidationProfile : SwarmHostOptionsValidationProfile<SwarmHostOptions>
    {
        /// <inheritdoc cref="SwarmHostOptionsValidationProfile"/>
        public SwarmHostOptionsValidationProfile() : base()
        {

        }
    }
}
