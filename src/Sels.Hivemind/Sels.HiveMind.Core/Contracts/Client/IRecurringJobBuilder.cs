using Sels.HiveMind.Job;
using System;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Job.Recurring;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for configuring additional options on background jobs during creation.
    /// </summary>
    public interface IRecurringJobBuilder : IJobBuilder<IRecurringJobBuilder>
    {
        /// <summary>
        /// The client used to create the background job.
        /// </summary>
        IRecurringJobClient Client { get; }
        /// <summary>
        /// The state that the recurring job will transition into after creation or when updated. 
        /// </summary>
        public IRecurringJobState ElectionState { get; }
        /// <summary>
        /// The current settings of the recurring job.
        /// </summary>
        public IRecurringJobSettings Settings { get; }
        /// <summary>
        /// The current update behaviour of the recurring job.
        /// </summary>
        public RecurringJobUpdateBehaviour UpdateBehaviour { get; }
        /// <summary>
        /// The current update timeout of the recurring job.
        /// </summary>
        public TimeSpan? UpdateTimeout { get; }
        /// <summary>
        /// The current update polling interval of the recurring job.
        /// </summary>
        public TimeSpan? UpdatePollingInterval { get; }
        /// <summary>
        /// The current creation/update requester of the recurring job.
        /// </summary>
        public string Requester { get; }
        /// <summary>
        /// The current schedule of the recurring job.
        /// </summary>
        public ISchedule Schedule { get; }
        /// <summary>
        /// If previous properties should be cleared when updating.
        /// </summary>
        public bool ClearPropertiesDuringUpdate { get; }

        /// <summary>
        /// Configures the schedule of the recurring job.
        /// </summary>
        /// <param name="scheduleBuilder">Delegate used to configure the schedule</param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder WithSchedule(Action<IScheduleBuilder> scheduleBuilder);

        /// <summary>
        /// Changes the state of the job to <paramref name="state"/> through state election during creation.
        /// Depending on the state election, the final state after creation might not be <paramref name="state"/>.
        /// State election will only be triggered if the recurring job is created, not updated.
        /// </summary>
        /// <param name="state">The state to elect</param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder InState(IRecurringJobState state);

        /// <summary>
        /// Overwrites the default settings of the recurring job.
        /// </summary>
        /// <param name="configurator">Delegate used to modify the default setttings</param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder Configure(Action<RecurringJobSettings> configurator);

        /// <summary>
        /// Who is requesting the the creation or update. Will be used to lock the job.
        /// Default is random guid.
        /// </summary>
        /// <param name="requester">Who is requesting the update</param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder RequestedBy(string requester);

        /// <summary>
        /// Dictates the behaviour when dealing with a recurring job that is running.
        /// </summary>
        /// <param name="behaviour">The behaviour to use</param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder UsingUpdateBehaviour(RecurringJobUpdateBehaviour behaviour);
        /// <summary>
        /// The maximum amount of time to wait for a recurring job to update.
        /// Can be set to null to wait indefinitely.
        /// </summary>
        /// <param name="timeout">The maximum amount of time to wait</param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder UsingUpdateTimeout(TimeSpan? timeout);
        /// <summary>
        /// How often to try and lock a recurring job if it is running so it can be updated.
        /// When set to null an optimal polling interval will try to be determined.
        /// </summary>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder UsingUpdatePollingInterval(TimeSpan? interval);

        /// <summary>
        /// If previous properties should be cleared when updating.
        /// </summary>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder ClearProperties();

        // Overloads
        /// <summary>
        /// Defines a middleware to use when executing the job.
        /// </summary>
        /// <typeparam name="T">The type of the middleware to add</typeparam>
        /// <param name="context"><inheritdoc cref="IMiddlewareInfo.Context"/></param>
        /// <param name="priority"><inheritdoc cref="IMiddlewareInfo.Priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        IRecurringJobBuilder WithMiddleWare<T>(object? context = null, byte? priority = null) where T : class, IRecurringJobMiddleware => WithMiddleWare(typeof(T), context, priority);
    }
}
