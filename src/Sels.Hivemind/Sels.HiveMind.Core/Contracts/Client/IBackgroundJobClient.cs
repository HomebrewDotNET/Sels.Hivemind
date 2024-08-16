using Sels.HiveMind.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Query.Job;
using Sels.Core.Extensions;
using Sels.Core;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.State.Background;
using Sels.HiveMind.Job.State;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Client for creating, fetching and querying background jobs.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobClient : IJobClient<IReadOnlyBackgroundJob, ILockedBackgroundJob, QueryBackgroundJobOrderByTarget?>
    {
        #region Create
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(IClientConnection connection, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
            => CreateAsync(connection.StorageConnection, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(IStorageConnection connection, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class;
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync<T>([Traceable(HiveLog.Environment)] string environment, Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync<T>(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(Expression<Func<T, object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class => CreateAsync<T>(HiveMindConstants.DefaultEnvironmentName, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(IClientConnection connection, Expression<Action<T>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
            => CreateAsync<T>(connection.StorageConnection, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(IStorageConnection connection, Expression<Action<T>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class;
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="environment">The HiveMind environment to create the job in</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync<T>(string environment, Expression<Action<T>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync<T>(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job of type <typeparamref name="T"/>.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync<T>(Expression<Action<T>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) where T : class => CreateAsync<T>(HiveMindConstants.DefaultEnvironmentName, methodSelector, jobBuilder, token);

        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(IClientConnection connection, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
             => CreateAsync(connection.StorageConnection, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(IStorageConnection connection, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync(string environment, Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(Expression<Func<object>> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) => CreateAsync(HiveMindConstants.DefaultEnvironmentName, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(IClientConnection connection, Expression<Action> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
            => CreateAsync(connection.StorageConnection, methodSelector, jobBuilder, token);
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="connection">Connection/transaction to execute the request in</param>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(IStorageConnection connection, Expression<Action> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default);
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public async Task<string> CreateAsync(string environment, Expression<Action> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);

            await using (var connection = await OpenConnectionAsync(environment, true, token).ConfigureAwait(false))
            {
                var job = await CreateAsync(connection, methodSelector, jobBuilder, token).ConfigureAwait(false);
                await connection.CommitAsync(token).ConfigureAwait(false);
                return job;
            }
        }
        /// <summary>
        /// Creates a new background job that calls a static method.
        /// Action will be executed on the default HiveMind environment.
        /// </summary>
        /// <typeparam name="T">The type of the background job to create</typeparam>
        /// <param name="jobBuilder">Delegate used to configure the job to create</param>
        /// <param name="methodSelector">Expression that selects the method on <typeparamref name="T"/> to execute</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The id of the created job</returns>
        public Task<string> CreateAsync(Expression<Action> methodSelector, Func<IBackgroundJobBuilder, IBackgroundJobBuilder> jobBuilder = null, CancellationToken token = default) => CreateAsync(HiveMindConstants.DefaultEnvironmentName, methodSelector, jobBuilder, token);
        #endregion
    }

    /// <summary>
    /// Builder for configuring additional options on background jobs during creation.
    /// </summary>
    public interface IBackgroundJobBuilder : IJobBuilder<IBackgroundJobBuilder>
    {
        /// <summary>
        /// The client used to create the background job.
        /// </summary>
        IBackgroundJobClient Client { get; }

        /// <summary>
        /// Changes the state of the job to <paramref name="state"/> through state election during creation.
        /// Depending on the state election, the final state after creation might not be <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The state to elect</param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder InState(IBackgroundJobState state);

        // Overloads
        /// <summary>
        /// Defines a middleware to use when executing the job.
        /// </summary>
        /// <typeparam name="T">The type of the middleware to add</typeparam>
        /// <param name="context"><inheritdoc cref="IMiddlewareInfo.Context"/></param>
        /// <param name="priority"><inheritdoc cref="IMiddlewareInfo.Priority"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder WithMiddleWare<T>(object? context = null, byte? priority = null) where T : class, IBackgroundJobMiddleware => WithMiddleWare(typeof(T), context, priority);
        /// <summary>
        /// Background job will only be executed after <paramref name="date"/>.
        /// State will be changed to <see cref="EnqueuedState"/>.
        /// </summary>
        /// <param name="date">The date after which the job can be picked up</param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder DelayExecutionTo(DateTime date)
        {
            return InState(new EnqueuedState(date));
        }

        #region Awaiting
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStates"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStates"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="AwaitingState.DeleteOnOtherState"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, IEnumerable<string> validStates, bool deleteOnOtherState = false)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));

            return InState(new AwaitingState(jobId, validStates, deleteOnOtherState));
        }
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStates"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStates"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="AwaitingState.DeleteOnOtherState"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, BackgroundJobContinuationStates validStates, bool deleteOnOtherState = false)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));

            if(validStates.HasFlag(BackgroundJobContinuationStates.Any)) return EnqueueAfter(jobId, null, deleteOnOtherState);

            List<string> validStateNames = new List<string>();

            if (validStates.HasFlag(BackgroundJobContinuationStates.Succeeded)) validStateNames.Add(SucceededState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Failed)) validStateNames.Add(FailedState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Deleted)) validStateNames.Add(DeletedState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Executing)) validStateNames.Add(ExecutingState.StateName);
            if (validStates.HasFlag(BackgroundJobContinuationStates.Finished))
            {
                validStateNames.Add(SucceededState.StateName);
                validStateNames.Add(FailedState.StateName);
            }

            return EnqueueAfter(jobId, validStateNames, deleteOnOtherState);
        }
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStateName"/> and <paramref name="validStateNames"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStateName"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="validStateNames"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="deleteOnOtherState"><inheritdoc cref="AwaitingState.DeleteOnOtherState"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, bool deleteOnOtherState, string validStateName, params string[] validStateNames)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));
            validStateName.ValidateArgumentNotNullOrWhitespace(nameof(validStateName));

            return EnqueueAfter(jobId, Helper.Collection.Enumerate(validStateName, validStateNames), deleteOnOtherState);
        }
        /// <summary>
        /// Enqueues the current job after parent job <paramref name="jobId"/> enters any state in <paramref name="validStateName"/> and <paramref name="validStateNames"/>.
        /// State will be changed to <see cref="AwaitingState"/>.
        /// </summary>
        /// <param name="jobId">The id of the job to wait on</param>
        /// <param name="validStateName"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <param name="validStateNames"><inheritdoc cref="AwaitingState.ValidStates"/></param>
        /// <returns>Current builder for method chaining</returns>
        IBackgroundJobBuilder EnqueueAfter(string jobId, string validStateName, params string[] validStateNames)
        {
            jobId.ValidateArgumentNotNullOrWhitespace(nameof(jobId));
            validStateName.ValidateArgumentNotNullOrWhitespace(nameof(validStateName));

            return EnqueueAfter(jobId, false, validStateName, validStateNames);
        }
        #endregion
    }

    /// <summary>
    /// Contains the states after which an awaiting job can transition into the <see cref="EnqueuedState"/>.
    /// </summary>
    [Flags]
    public enum BackgroundJobContinuationStates
    {
        /// <summary>
        /// Continue job if parent job transitions into any state.
        /// </summary>
        Any = 1,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="SucceededState"/>.
        /// </summary>
        Succeeded = 2,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="FailedState"/>.
        /// </summary>
        Failed = 4,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="DeletedState"/>.
        /// </summary>
        Deleted = 8,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="SucceededState"/> or <see cref="FailedState"/>.
        /// </summary>
        Finished = 16,
        /// <summary>
        /// Continue job if parent job transitions into the <see cref="ExecutingState"/>.
        /// </summary>
        Executing = 32,
    }
}
