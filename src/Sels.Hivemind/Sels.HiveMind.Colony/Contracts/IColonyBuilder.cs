using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core.Async.TaskManagement;
using Sels.Core.Extensions;
using Sels.HiveMind.Calendar;
using Sels.HiveMind.Client;
using Sels.HiveMind.Colony.SystemDaemon;
using Sels.HiveMind.Colony.Options;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Colony.Templates;
using Sels.HiveMind.Interval;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Extensions.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.Core.Mediator;
using Sels.HiveMind.Colony.Swarm.Job.Background;
using Sels.HiveMind.Colony.Swarm.Job.Recurring;
using Sels.HiveMind.DistributedLocking;

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
        /// Sets <see cref="IColonyInfo.Id"/>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IColonyBuilder WithId(string id);
        /// <summary>
        /// Sets <see cref="IColony.Name"/>.
        /// </summary>
        /// <param name="name">The display name for the colony</param>
        /// <returns>Current builder for method chaining</returns>
        IColonyBuilder WithName(string name);
        /// <summary>
        /// Sets <see cref="IColonyInfo.Environment"/>.
        /// </summary>
        /// <param name="environment">The HiveMind environment to connect to</param>
        /// <returns>Current builder for method chaining</returns>
        IColonyBuilder InEnvironment(string environment);
        /// <summary>
        /// Sets the options for this instance.
        /// </summary>
        /// <param name="options">The options to set</param>
        /// <returns>Current builder for method chaining</returns>
        IColonyBuilder WithOptions(ColonyOptions options);
    }

    /// <summary>
    /// Allows for the configuration of an instance of <see cref="IColony"/> during creation.
    /// </summary>
    public interface IColonyBuilderConfigurator : IColonyConfigurator<IColonyBuilderConfigurator>
    {
    }
    /// <summary>
    /// Allows for the configuration of an instance of <see cref="IColony"/> post creation.
    /// </summary>
    public interface IColonyConfigurator : IColonyConfigurator<IColonyConfigurator>
    {
        /// <summary>
        /// Removes daemon <paramref name="name"/> from the colony.
        /// If it's running the daemon will be stopped.
        /// </summary>
        /// <param name="name"><see cref="IDaemonInfo.Name"/> of the daemon to remove</param>
        /// <returns>Current configurator for method chaining</returns>
        IColonyConfigurator RemoveDaemon(string name);
        /// <summary>
        /// Changes <see cref="IColonyInfo.Name"/> to <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The new display name to set</param>
        /// <returns>Current configurator for method chaining</returns>
        IColonyConfigurator ChangeName(string name);
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
        T WithDaemon(string name, Func<IDaemonExecutionContext, CancellationToken, Task> runDelegate, Action<IDaemonBuilder>? builder = null);
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
        T WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, TInstance>? constructor, bool? allowDispose = null, Action<IDaemonBuilder>? builder = null)
            => WithDaemon<TInstance>(name, runDelegate, constructor != null ? new Func<IServiceProvider, IDaemonExecutionContext, TInstance>((p, c) => constructor(p)) : null, allowDispose, builder);
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
        T WithDaemon<TInstance>(string name, Func<TInstance, IDaemonExecutionContext, CancellationToken, Task> runDelegate, Func<IServiceProvider, IDaemonExecutionContext, TInstance>? constructor = null, bool? allowDispose = null, Action<IDaemonBuilder>? builder = null);
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
        T WithDaemonExecutor<TInstance>(string name, Func<IServiceProvider, TInstance> constructor, bool? allowDispose = null, Action<IDaemonBuilder>? builder = null) where TInstance : IDaemonExecutor
        => WithDaemon<TInstance>(name, (i, c, t) => i.RunUntilCancellation(c, t), constructor, allowDispose, builder);
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
        T WithDaemonExecutor<TInstance>(string name, Func<IServiceProvider, IDaemonExecutionContext, TInstance>? constructor = null, bool? allowDispose = null, Action<IDaemonBuilder>? builder = null) where TInstance : IDaemonExecutor
        => WithDaemon<TInstance>(name, (i, c, t) => i.RunUntilCancellation(c, t), constructor, allowDispose, builder);

        #region Swarms
        #region BackgroundJob
        /// <summary>
        ///  Adds a new daemon that hosts worker swarms for executing background jobs.
        /// </summary>
        /// <param name="swarmName">The name of the root swarm</param>
        /// <param name="swarmBuilder">Builder for configuring the worker swarms</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <param name="scheduleBuilder">Option delegate that can be used to set the schedule the swarm should run as</param>
        /// <param name="scheduleBehaviour">The schedule behaviour of the daemon</param>
        /// <returns>Current builder for method chaining</returns>
        T WithWorkerSwarm(string swarmName, Action<BackgroundJobWorkerSwarmHostOptions>? swarmBuilder = null, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            swarmName.ValidateArgumentNotNullOrWhitespace(nameof(swarmName));

            var options = new BackgroundJobWorkerSwarmHostOptions(swarmName, swarmBuilder ?? new Action<BackgroundJobWorkerSwarmHostOptions>(x => { }));
            return WithWorkerSwarm(options, daemonBuilder, daemonName, scheduleBuilder, scheduleBehaviour);
        }
        /// <summary>
        /// Adds a new daemon that hosts worker swarms for executing background jobs.
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <param name="scheduleBuilder">Option delegate that can be used to set the schedule the swarm should run as</param>
        /// <param name="scheduleBehaviour">The schedule behaviour of the daemon</param>
        /// <returns>Current builder for method chaining</returns>
        T WithWorkerSwarm(BackgroundJobWorkerSwarmHostOptions options, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            options.ValidateArgument(nameof(options));

            var swarmDaemonBuilder = new Action<IDaemonBuilder>(x =>
            {
                x.WithPriority(128)
                 .WithRestartPolicy(DaemonRestartPolicy.UnlessStopped);

                daemonBuilder?.Invoke(x);
            });

            BackgroundJobWorkerSwarmHostOptionsValidationProfile.Instance.Validate(options).ThrowOnValidationErrors();

            return WithDaemon<BackgroundJobWorkerSwarmHost>(daemonName ?? $"WorkerSwarmHost.{options.Name}", (h, c, t) => h.RunUntilCancellation(c, t), x =>
            {
                return new BackgroundJobWorkerSwarmHost(options,
                                           x.GetRequiredService<IBackgroundJobClient>(),
                                           HiveMindConstants.Queue.BackgroundJobProcessQueueType,
                                           x.GetRequiredService<IOptionsMonitor<BackgroundJobWorkerSwarmDefaultHostOptions>>(),
                                           x.GetRequiredService<IJobQueueProvider>(),
                                           x.GetRequiredService<IJobSchedulerProvider>(),
                                           scheduleBuilder ?? new Action<IScheduleBuilder>(x => { }),
                                           scheduleBehaviour,
                                           x.GetRequiredService<ITaskManager>(),
                                           x.GetRequiredService<IIntervalProvider>(),
                                           x.GetRequiredService<ICalendarProvider>(),
                                           x.GetRequiredService<ScheduleValidationProfile>(),
                                           x.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                           x.GetService<IMemoryCache>(),
                                           x.GetService<ILogger<BackgroundJobWorkerSwarmHost>>()
                );
            }, true, swarmDaemonBuilder);
        }
        #endregion
        #region RecurringJob
        /// <summary>
        ///  Adds a new daemon that hosts worker swarms for executing recurring jobs.
        /// </summary>
        /// <param name="swarmName">The name of the root swarm</param>
        /// <param name="swarmBuilder">Builder for configuring the worker swarms</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <param name="scheduleBuilder">Option delegate that can be used to set the schedule the swarm should run as</param>
        /// <param name="scheduleBehaviour">The schedule behaviour of the daemon</param>
        /// <returns>Current builder for method chaining</returns>
        T WithRecurringJobWorkerSwarm(string swarmName, Action<RecurringJobWorkerSwarmHostOptions>? swarmBuilder = null, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            swarmName.ValidateArgumentNotNullOrWhitespace(nameof(swarmName));

            var options = new RecurringJobWorkerSwarmHostOptions(swarmName, swarmBuilder ?? new Action<RecurringJobWorkerSwarmHostOptions>(x => { }));
            return WithRecurringJobWorkerSwarm(options, daemonBuilder, daemonName, scheduleBuilder, scheduleBehaviour);
        }
        /// <summary>
        /// Adds a new daemon that hosts worker swarms for executing recurring jobs.
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <param name="scheduleBuilder">Option delegate that can be used to set the schedule the swarm should run as</param>
        /// <param name="scheduleBehaviour">The schedule behaviour of the daemon</param>
        /// <returns>Current builder for method chaining</returns>
        T WithRecurringJobWorkerSwarm(RecurringJobWorkerSwarmHostOptions options, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            options.ValidateArgument(nameof(options));

            var swarmDaemonBuilder = new Action<IDaemonBuilder>(x =>
            {
                x.WithPriority(128)
                 .WithRestartPolicy(DaemonRestartPolicy.UnlessStopped);

                daemonBuilder?.Invoke(x);
            });

            RecurringJobWorkerSwarmHostOptionsValidationProfile.Instance.Validate(options).ThrowOnValidationErrors();

            return WithDaemon<RecurringJobWorkerSwarmHost>(daemonName ?? $"RecurringJobWorkerSwarmHost.{options.Name}", (h, c, t) => h.RunUntilCancellation(c, t), x =>
            {
                return new RecurringJobWorkerSwarmHost(options,
                                           x.GetRequiredService<IRecurringJobClient>(),
                                           HiveMindConstants.Queue.RecurringJobProcessQueueType,
                                           x.GetRequiredService<IOptionsMonitor<RecurringJobWorkerSwarmDefaultHostOptions>>(),
                                           x.GetRequiredService<IJobQueueProvider>(),
                                           x.GetRequiredService<IJobSchedulerProvider>(),
                                           scheduleBuilder ?? new Action<IScheduleBuilder>(x => { }),
                                           scheduleBehaviour,
                                           x.GetRequiredService<ITaskManager>(),
                                           x.GetRequiredService<IIntervalProvider>(),
                                           x.GetRequiredService<ICalendarProvider>(),
                                           x.GetRequiredService<ScheduleValidationProfile>(),
                                           x.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                           x.GetService<IMemoryCache>(),
                                           x.GetService<ILogger<RecurringJobWorkerSwarmHost>>()
                );
            }, true, swarmDaemonBuilder);
        }
        #endregion
        #endregion

        #region Deletion
        /// <summary>
        ///  Adds a new daemon that deletes background jobs using mode <see cref="DeletionMode.System"/>.
        /// </summary>
        /// <param name="swarmName">The name of the root swarm</param>
        /// <param name="deletionDaemonBuilder">Builder for configuring the worker swarms</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <returns>Current builder for method chaining</returns>
        T WithSystemDeletingDeletionDaemon(Action<DeletionDeamonOptions>? deletionDaemonBuilder = null, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            DeletionDeamonOptions? options = null;
            if (deletionDaemonBuilder != null)
            {
                options = new DeletionDeamonOptions();
                deletionDaemonBuilder.Invoke(options);
            }

            return WithSystemDeletingDeletionDaemon(options, daemonBuilder, daemonName, scheduleBuilder, scheduleBehaviour);
        }
        /// <summary>
        /// Adds a new daemon that deletes background jobs using mode <see cref="DeletionMode.System"/>.
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <returns>Current builder for method chaining</returns>
        T WithSystemDeletingDeletionDaemon(DeletionDeamonOptions? options, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            var swarmDaemonBuilder = new Action<IDaemonBuilder>(x =>
            {
                x.WithPriority(127)
                 .WithRestartPolicy(DaemonRestartPolicy.UnlessStopped);

                daemonBuilder?.Invoke(x);
            });

            if (options != null) options.ValidateAgainstProfile<DeletionDeamonOptionsValidationProfile, DeletionDeamonOptions, string>().ThrowOnValidationErrors();

            var name = daemonName ?? "DeletionDaemon";
            return WithDaemon<SystemDeletingDeletionDaemon>(name, (h, c, t) => h.RunUntilCancellation(c, t), (x, c) =>
            {
                return new SystemDeletingDeletionDaemon(x.GetRequiredService<INotifier>(),
                                          options ?? x.GetRequiredService<IOptionsSnapshot<DeletionDeamonOptions>>().Get($"{c.Daemon.Colony.Environment}.{name}"),
                                          x.GetRequiredService<IBackgroundJobClient>(),
                                          x.GetRequiredService<DeletionDeamonOptionsValidationProfile>(),
                                          scheduleBuilder ?? new Action<IScheduleBuilder>(x => x.RunEvery(TimeSpan.FromHours(1))),
                                          scheduleBehaviour,
                                          x.GetRequiredService<ITaskManager>(),
                                          x.GetRequiredService<IIntervalProvider>(),
                                          x.GetRequiredService<ICalendarProvider>(),
                                          x.GetRequiredService<ScheduleValidationProfile>(),
                                          x.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                          x.GetService<IMemoryCache>(),
                                          x.GetService<ILogger<SystemDeletingDeletionDaemon>>()
                );
            }, true, swarmDaemonBuilder);
        }
        /// <summary>
        ///  Adds a new daemon that deletes background jobs using mode <see cref="DeletionMode.Bulk"/>.
        /// </summary>
        /// <param name="swarmName">The name of the root swarm</param>
        /// <param name="deletionDaemonBuilder">Builder for configuring the worker swarms</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <returns>Current builder for method chaining</returns>
        T WithBulkDeletionDaemon(Action<DeletionDeamonOptions>? deletionDaemonBuilder = null, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            DeletionDeamonOptions? options = null;
            if (deletionDaemonBuilder != null)
            {
                options = new DeletionDeamonOptions();
                deletionDaemonBuilder.Invoke(options);
            }

            return WithBulkDeletionDaemon(options, daemonBuilder, daemonName, scheduleBuilder, scheduleBehaviour);
        }
        /// <summary>
        /// Adds a new daemon that deletes background jobs using mode <see cref="DeletionMode.Bulk"/>.
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="daemonBuilder">Optional delegate for configuring the daemon</param>
        /// <param name="daemonName">Optional name for the deamon. When set to null the swarm name will be used</param>
        /// <returns>Current builder for method chaining</returns>
        T WithBulkDeletionDaemon(DeletionDeamonOptions? options, Action<IDaemonBuilder>? daemonBuilder = null, string? daemonName = null, Action<IScheduleBuilder>? scheduleBuilder = null, ScheduleDaemonBehaviour scheduleBehaviour = ScheduleDaemonBehaviour.InstantStart | ScheduleDaemonBehaviour.StopIfOutsideOfSchedule)
        {
            var swarmDaemonBuilder = new Action<IDaemonBuilder>(x =>
            {
                x.WithPriority(127)
                 .WithRestartPolicy(DaemonRestartPolicy.UnlessStopped);

                daemonBuilder?.Invoke(x);
            });

            if (options != null) options.ValidateAgainstProfile<DeletionDeamonOptionsValidationProfile, DeletionDeamonOptions, string>().ThrowOnValidationErrors();

            var name = daemonName ?? "DeletionDaemon";
            return WithDaemon<BulkDeletingDeletionDaemon>(name, (h, c, t) => h.RunUntilCancellation(c, t), (x, c) =>
            {
                return new BulkDeletingDeletionDaemon(x.GetRequiredService<INotifier>(),
                                          options ?? x.GetRequiredService<IOptionsSnapshot<DeletionDeamonOptions>>().Get($"{c.Daemon.Colony.Environment}.{name}"),
                                          x.GetRequiredService<IBackgroundJobClient>(),
                                          x.GetRequiredService<IDistributedLockServiceProvider>(),
                                          x.GetRequiredService<DeletionDeamonOptionsValidationProfile>(),
                                          scheduleBuilder ?? new Action<IScheduleBuilder>(x => x.RunEvery(TimeSpan.FromHours(1))),
                                          scheduleBehaviour,
                                          x.GetRequiredService<ITaskManager>(),
                                          x.GetRequiredService<IIntervalProvider>(),
                                          x.GetRequiredService<ICalendarProvider>(),
                                          x.GetRequiredService<ScheduleValidationProfile>(),
                                          x.GetRequiredService<IOptionsMonitor<HiveMindOptions>>(),
                                          x.GetService<IMemoryCache>(),
                                          x.GetService<ILogger<SystemDeletingDeletionDaemon>>()
                );
            }, true, swarmDaemonBuilder);
        }

        #endregion
    }

    /// <summary>
    /// Builder for configuring additional options on a <see cref="IDaemon"/>.
    /// </summary>
    public interface IDaemonBuilder
    {
        /// <summary>
        /// Sets <see cref="IReadOnlyDaemon.AutoStart"/>.
        /// </summary>
        /// <returns>Current builder for method chaining</returns>
        IDaemonBuilder DisableAutoStart();
        /// <summary>
        /// Sets <see cref="IReadOnlyDaemon.Priority"/>.
        /// </summary>
        /// <param name="priority">The priority to set</param>
        /// <returns>Current builder for method chaining</returns>
        IDaemonBuilder WithPriority(byte priority);
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
