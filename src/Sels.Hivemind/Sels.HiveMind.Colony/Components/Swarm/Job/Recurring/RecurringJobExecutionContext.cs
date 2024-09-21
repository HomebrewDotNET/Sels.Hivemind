using Sels.Core.Async.TaskManagement;
using Sels.HiveMind.Colony.Templates.Swarm.Job;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Storage.Job.Recurring;
using Sels.HiveMind.Service;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.Recurring
{
    /// <inheritdoc cref="IRecurringJobExecutionContext"/>
    public class RecurringJobExecutionContext : JobExecutionContext<IRecurringJobWorkerSwarmHostOptions, IWriteableRecurringJob, IRecurringJobAction, RecurringJobStorageData, IRecurringJobState, JobStateStorageData>, IRecurringJobExecutionContext
    {
        /// <inheritdoc cref="RecurringJobExecutionContext"/>
        /// <param name="daemonContext">The context of the daemon currently executing the job</param>
        /// <param name="droneState">The state of the drone executing <paramref name="job"/></param>
        /// <param name="job"><inheritdoc cref="IJobExecutionContext{TJob}.Job"/></param>
        /// <param name="jobInstance"><inheritdoc cref="IJobExecutionContext{TJob}.JobInstance"/></param>
        /// <param name="invocationArguments"><inheritdoc cref="IJobExecutionContext{TJob}.InvocationArguments"/></param>
        /// <param name="enabledLogLevel">The log level above which to persist logs created by the executing recurring job</param>
        /// <param name="logFlushInterval">How often to flush logs to storage</param>
        /// <param name="storage">The storage to use to persist state</param>
        /// <param name="taskManager">The task manager to use to manage recurring tasks</param>
        /// <param name="service">Used to fetch action for the executing recurring job</param>
        /// <param name="actionFetchLimit">How many actions can be pulled into memory at the same time</param>
        /// <param name="actionInterval">How often to check for pending actions for the recurring job</param>
        /// <param name="activator">Used to activate pending actions</param>
        /// <param name="jobCancellationSource">Used to cancel the running job</param>
        /// <param name="loggerFactory">Used to create the ILogger to use to trace logs created by the recurring job</param>
        public RecurringJobExecutionContext(IDaemonExecutionContext daemonContext, IDroneState<IRecurringJobWorkerSwarmHostOptions> droneState, IWriteableRecurringJob job, object? jobInstance, object[] invocationArguments, CancellationTokenSource jobCancellationSource, LogLevel enabledLogLevel, TimeSpan logFlushInterval, TimeSpan actionInterval, int actionFetchLimit, IRecurringJobService service, IActivatorScope activator, ITaskManager taskManager, IStorage storage, ILoggerFactory? loggerFactory)
        : base(daemonContext, droneState, job, jobInstance, invocationArguments, jobCancellationSource, enabledLogLevel, logFlushInterval, actionInterval, actionFetchLimit, service, activator, taskManager, storage, loggerFactory)
        {

        }
        /// <inheritdoc/>
        protected override async Task ExecuteActionAsync(IRecurringJobAction action, ActionInfo actionInfo, CancellationToken token)
        {
            action = Guard.IsNotNull(action);
            actionInfo = Guard.IsNotNull(actionInfo);

            await action.ExecuteAsync(this, actionInfo.Context, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        protected override Task PersistLogs(IStorageConnection storageConnection, string jobId, IEnumerable<LogEntry> logEntries, CancellationToken token)
        {
            return storageConnection.Storage.CreateRecurringJobLogsAsync(storageConnection, Job.Id, logEntries, token);
        }
    }
}
