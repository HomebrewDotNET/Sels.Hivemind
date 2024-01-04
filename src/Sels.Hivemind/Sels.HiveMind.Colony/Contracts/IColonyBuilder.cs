using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Scheduler;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Building for configuring an instance of <see cref="IColony"/>
    /// </summary>
    public interface IColonyBuilder : IColonyConfigurator<IColonyBuilder>
    {
        /// <summary>
        /// Current state of the colony being created.
        /// </summary>
        IReadOnlyColony Current { get; }

        /// <summary>
        /// Sets <see cref="IColony.Name"/>.
        /// </summary>
        /// <param name="name">The globally unique name for the colony</param>
        /// <returns>Current builder for method chaining</returns>
        IColonyBuilder WithName(string name);
        /// <summary>
        /// Sets <see cref="IColony.Environment"/>.
        /// </summary>
        /// <param name="environment">The HiveMind environment to connect to</param>
        /// <returns>Current builder for method chaining</returns>
        IColonyBuilder InEnvironment(string environment);
        /// <summary>
        /// Sets the options for this instance.
        /// </summary>
        /// <param name="options">The options to set</param>
        /// <returns>Current builder for method chaining</returns>
        IColonyBuilder WithOptions(HiveColonyOptions options);
    }

    /// <summary>
    /// Allows for the configuration of an instance of <see cref="IColony"/>.
    /// </summary>
    public interface IColonyConfigurator : IColonyConfigurator<IColonyConfigurator>
    {
    }

    /// <summary>
    /// Allows for the configuration of an instance of <see cref="IColony"/>.
    /// </summary>
    /// <typeparam name="T">The type to return for the fluent syntax</typeparam>
    public interface IColonyConfigurator<T>
    {
        /// <summary>
        /// Adds a new daemon that will be managed by the colony.
        /// Daemon will execute an anonymous delegate.
        /// </summary>
        /// <param name="name"><see cref="IDaemon.Name"/></param>
        /// <param name="runDelegate">The delegate that will be called to execute the daemon</param>
        /// <param name="builder">Optional delegate for setting additonal options</param>
        /// <returns>Current builder for method chaining</returns>
        T WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder> builder = null);
        /// <summary>
        /// Adds a new daemon that will be managed by the colony.
        /// Daemon will execute an instance of <typeparamref name="TInstance"/>.
        /// </summary>
        /// <typeparam name="TInstance">The type of the instance that the daemon can execute</typeparam>
        /// <param name="name"><see cref="IDaemon.Name"/></param>
        /// <param name="runDelegate">The delegate that will be called to execute the daemon</param>
        /// <param name="constructor">Optional delegate that creates the instance to execute</param>
        /// <param name="allowDispose">If <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/> needs to be called on <typeparamref name="T"/> if implemented. When set to null disposing will be determined based on the constructor used</param>
        /// <param name="builder">Optional delegate for setting additonal options</param>
        /// <returns>Current builder for method chaining</returns>
        T WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, TInstance> constructor = null, bool? allowDispose = null, Action<IDaemonBuilder> builder = null);
        /// <summary>
        /// Adds a new daemon that will be managed by the colony.
        /// Daemon will execute an instance of <typeparamref name="TInstance"/>.
        /// </summary>
        /// <typeparam name="TInstance">The type of the instance that the daemon can execute</typeparam>
        /// <param name="name"><see cref="IDaemon.Name"/></param>
        /// <param name="runDelegate">The delegate that will be called to execute the daemon</param>
        /// <param name="constructor">Optional delegate that creates the instance to execute</param>
        /// <param name="allowDispose">If <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/> needs to be called on <typeparamref name="T"/> if implemented. When set to null disposing will be determined based on the constructor used</param>
        /// <param name="builder">Optional delegate for setting additonal options</param>
        /// <returns>Current builder for method chaining</returns>
        T WithDaemonExecutor<TInstance>(string name, Func<IServiceProvider, TInstance> constructor = null, bool? allowDispose = null, Action<IDaemonBuilder> builder = null) where TInstance : IDaemonExecutor
        => WithDaemon<TInstance>(name, (i, c, t) => i.RunUntilCancellation(c, t), constructor, allowDispose, builder);

        #region Swarms
        #region Worker
        /// <summary>
        ///  Adds a new daemon that hosts worker swarms for executing background jobs.
        /// </summary>
        /// <param name="swarmName">The name of the root swarm</param>
        /// <param name="swarmBuilder">Builder for configuring the worker swarms</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <returns>Current builder for method chaining</returns>
        T WithWorkerSwarm(string swarmName, Action<WorkerSwarmHostOptions> swarmBuilder = null, Action<IDaemonBuilder> daemonBuilder = null)
        {
            swarmName.ValidateArgumentNotNullOrWhitespace(nameof(swarmName));

            var options = new WorkerSwarmHostOptions(swarmName, swarmBuilder ?? new Action<WorkerSwarmHostOptions>(x => { }));
            return WithWorkerSwarm(options, daemonBuilder);
        }
        /// <summary>
        /// Adds a new daemon that hosts worker swarms for executing background jobs.
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <returns>Current builder for method chaining</returns>
        T WithWorkerSwarm(WorkerSwarmHostOptions options, Action<IDaemonBuilder> daemonBuilder = null)
        {
            options.ValidateArgument(nameof(options));

            var swarmDaemonBuilder = new Action<IDaemonBuilder>(x =>
            {
                x.WithPriority(128)
                 .WithRestartPolicy(DaemonRestartPolicy.UnlessStopped);

                daemonBuilder?.Invoke(x);
            });

            options.ValidateAgainstProfile<WorkerSwarmHostOptionsValidationProfile, WorkerSwarmHostOptions, string>().ThrowOnValidationErrors();

            return WithDaemon<WorkerSwarmHost>($"WorkerSwarmHost.{options.Name}", (h, c, t) => h.RunAsync(c, t), x =>
            {
                return new WorkerSwarmHost(options,
                                           x.GetRequiredService<IOptionsMonitor<WorkerSwarmDefaultHostOptions>>(),
                                           x.GetRequiredService<ITaskManager>(),
                                           x.GetRequiredService<IJobQueueProvider>(),
                                           x.GetRequiredService<IJobSchedulerProvider>());
            }, true, swarmDaemonBuilder);
        }
        #endregion

        #endregion
    }

    /// <summary>
    /// Builder for configuring additional options on a <see cref="IDaemon"/>.
    /// </summary>
    public interface IDaemonBuilder
    {
        /// <summary>
        /// Sets <see cref="IReadOnlyDaemon.Priority"/>.
        /// </summary>
        /// <param name="priority">The priority to set</param>
        /// <returns>Current builder for method chaining</returns>
        IDaemonBuilder WithPriority(ushort priority);
        /// <summary>
        /// Sets <see cref="IReadOnlyDaemon.RestartPolicy"/>.
        /// </summary>
        /// <param name="restartPolicy">The restart policy to use</param>
        /// <returns>Current builder for method chaining</returns>
        IDaemonBuilder WithRestartPolicy(DaemonRestartPolicy restartPolicy);
        /// <summary>
        /// Sets <see cref="IReadOnlyDaemon.EnabledLogLevel"/>.
        /// </summary>
        /// <param name="logLevel">The log level to start persist logging from. Can be set to null to use the default defined on the colony</param>
        /// <returns>Current builder for method chaining</returns>
        IDaemonBuilder WithLogLevel(LogLevel? logLevel);
        /// <summary>
        /// Adds a new property to <see cref="IDaemon.LocalProperties"/>.
        /// </summary>
        /// <param name="name">The name of the property to add</param>
        /// <param name="value">The value for the property</param>
        /// <returns>Current builder for method chaining/returns>
        IDaemonBuilder WithLocalProperty(string name, object value);
        /// <summary>
        /// Adds new properties to <see cref="IDaemon.LocalProperties"/>.
        /// </summary>
        /// <param name="properties">Enumerator that returns the properties to add</param>
        /// <returns>Current builder for method chaining/returns>
        IDaemonBuilder WithLocalProperties(IEnumerable<KeyValuePair<string, object>> properties);
        /// <summary>
        /// Adds a new property to <see cref="IDaemon.Properties"/>.
        /// </summary>
        /// <param name="name">The name of the property to add</param>
        /// <param name="value">The value for the property</param>
        /// <returns>Current builder for method chaining/returns>
        IDaemonBuilder WithProperty(string name, object value);
        /// <summary>
        /// Adds new properties to <see cref="IDaemon.Properties"/>.
        /// </summary>
        /// <param name="properties">Enumerator that returns the properties to add</param>
        /// <returns>Current builder for method chaining/returns>
        IDaemonBuilder WithProperties(IEnumerable<KeyValuePair<string, object>> properties);
    }
}
