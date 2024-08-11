using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <inheritdoc cref="IWorkerSwarmHostOptions{TMiddleware, TOptions}"/>
    /// <typeparam name="TMiddleware">The interface type of the middleware used when executing jobs</typeparam>
    /// <typeparam name="TSelf">The top level type inheriting from this class</typeparam>
    /// <typeparam name="TOptions">The public readonly type of the current options</typeparam>
    public abstract class WorkerSwarmHostOptions<TMiddleware, TSelf, TOptions> : JobSwarmHostOptions<TSelf, TOptions>, IWorkerSwarmHostOptions<TMiddleware, TOptions>
        where TSelf : TOptions
        where TOptions : IWorkerSwarmHostOptions<TMiddleware, TOptions>
        where TMiddleware : class
    {


        // Properties
        /// <inheritdoc/>
        IReadOnlyList<ISwarmHostMiddlewareOptions<TMiddleware>> IWorkerSwarmHostOptions<TMiddleware, TOptions>.JobMiddleware => JobMiddleware;
        /// <inheritdoc cref="IWorkerSwarmHostOptions{TMiddleware, TOptions}.JobMiddleware"/>
        public List<SwarmHostMiddlewareOptions<TMiddleware>> JobMiddleware { get; set; }
        /// <inheritdoc/>
        public LogLevel? LogLevel { get; set; }
        /// <inheritdoc/>
        public TimeSpan? LogFlushInterval { get; set; }
        /// <inheritdoc/>
        public TimeSpan? ActionPollingInterval { get; set; }
        /// <inheritdoc/>
        public int? ActionFetchLimit { get; set; }

        /// <inheritdoc cref="WorkerSwarmHostOptions"/>
        public WorkerSwarmHostOptions() : base()
        {

        }

        /// <summary>
        /// Adds a job scheduler middleware that will be used by the job scheduler of this swarm.
        /// </summary>
        /// <typeparam name="T">The type of the middleware to add</typeparam>
        /// <param name="factory">Delegate that will be used to to create the middleware</param>
        /// <param name="context">Optional context that can be used to provide input to the middleware</param>
        /// <param name="configureMiddleware">Optional delegate for configuring the middleware</param>
        /// <returns>Current options for method chaining</returns>
        public TOptions AddJobMiddleware<T>(Func<IServiceProvider, Task<IComponent<T>>> factory, object? context = null, Action<SwarmHostMiddlewareConfigurationOptions>? configureMiddleware = null) where T : class, TMiddleware
        {
            factory = Guard.IsNotNull(factory);

            var options = new SwarmHostMiddlewareOptions<TMiddleware>()
            {
                Factory = async p => await factory(p).ConfigureAwait(false),
                Context = context
            };

            configureMiddleware?.Invoke(options.ConfigurationOptions);

            JobMiddleware ??= new List<SwarmHostMiddlewareOptions<TMiddleware>>();
            JobMiddleware.Add(options);
            return Self;
        }
    }

    /// <summary>
    /// Contains the validation rules <see cref="WorkerSwarmHostOptions{TMiddleware, TSelf, TOptions}"/>
    /// </summary>
    public class WorkerSwarmHostOptionsValidationProfile<TMiddleware, TSelf, TOptions> : JobSwarmHostOptionsValidationProfile<TSelf, TOptions>
        where TSelf : TOptions
        where TOptions : IWorkerSwarmHostOptions<TMiddleware, TOptions>
        where TMiddleware : class
    {
        /// <inheritdoc cref="WorkerSwarmHostOptionsValidationProfile{TMiddleware, TSelf, TOptions}"/>
        public WorkerSwarmHostOptionsValidationProfile() : base()
        {
            CreateValidationFor<WorkerSwarmHostOptions<TMiddleware, TSelf, TOptions>>()
                .ForProperty(x => x.ActionFetchLimit, x => x!.Value)
                    .MustBeLargerOrEqualTo(1)
                    .MustBeSmallerOrEqualTo(HiveMindConstants.Query.MaxDequeueLimit);

            CreateValidationFor<ISwarmHostMiddlewareOptions<TMiddleware>>()
                .ForProperty(x => x.Data)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Source.Factory == null, x => $"Can only be set when <{nameof(x.Source.Factory)}> is not set")
                .ForProperty(x => x.Data, x => x!.TypeName)
                    .ValidIf(x =>
                    {
                        if (!x.Value.HasValue()) return false;
                        var type = Type.GetType(x.Value, false);
                        if (type == null) return false;
                        return type.IsAssignableTo<TMiddleware>();
                    }, x => $"Must be assignable to <{typeof(TMiddleware)}>")
                .ForProperty(x => x.Factory)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Source.Data == null, x => $"Can only be set when <{nameof(x.Source.Data)}> is not set")
                .ForProperty(x => x.ConfigurationOptions)
                    .CannotBeNull();

            ImportFrom<SharedValidationProfile>();
        }
    }
}
