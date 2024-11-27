using Microsoft.Extensions.Logging;
using Sels.HiveMind.Job.Recurring;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains the configured options for a <see cref="IColonyInfo"/>.
    /// </summary>
    public interface IColonyOptions
    {
        /// <summary>
        /// How long the colony will wait before retrying a failure.
        /// </summary>
        public TimeSpan ErrorRetryDelay { get; }
        /// <summary>
        /// How long a colony has to try and release it's process lock before forcefully cancelling.
        /// </summary>
        public TimeSpan ReleaseLockTime { get; }
        /// <summary>
        /// How often the colony should check it's daemons (Start, restart, remove, ...)
        /// </summary>
        public TimeSpan DaemonManagementInterval { get; }
        /// <summary>
        /// How long a daemon has after sending the cancellation signal to finish it's task before it is considered <see cref="DaemonStatus.Timedout"/>.
        /// </summary>
        public TimeSpan DaemonMaxStopTime { get; }
        /// <summary>
        /// The default enabled log level for logs created by running daemons.
        /// </summary>
        public LogLevel DefaultDaemonLogLevel { get;}
        /// <inheritdoc cref="ColonyCreationOptions"/>
        public ColonyCreationOptions CreationOptions { get; }
        /// <summary>
        /// Defines which deletion daemon to use when the <see cref="ColonyCreationOptions.AutoCreateDeletionDaemon"/> option is enabled.
        /// </summary>
        public DeletionMode DeletionMode { get; set; }
        /// <summary>
        /// How often a colony will try to persist it's current state (and that of it's daemons including any new logs) to the storage.
        /// </summary>
        public TimeSpan StateSyncInterval { get; }
        /// <summary>
        /// How many times a scheduled daemon will attempt to generate the next schedule date. Used to avoid infinite loops when the schedule is invalid.
        /// </summary>
        public int MaxScheduleTries { get; }
        /// <summary>
        /// How long inactive (not locked or lost lock) colony state should be kept. When colonies have been inactive more than the retention they will be removed from storage.
        /// </summary>
        public TimeSpan InactiveColonyRetention { get; }
        /// <summary>
        /// How often to check for inactive colonies to delete.
        /// </summary>
        public TimeSpan InactiveColonyManagementInterval { get; }
        /// <summary>
        /// Defines when older logs from daemons should be removed.
        /// </summary>
        public ColonyDaemonRetentionMode DaemonLogRetentionMode { get; }
        /// <summary>
        /// The amount that will be applied based on the selected option in <see cref="DaemonLogRetentionMode"/>.
        /// For <see cref="RecurringJobRetentionMode.Amount"/> this will be the amount of logs that will be kept.
        /// For <see cref="RecurringJobRetentionMode.OlderThan"/> this will be the amount of days that will be kept.
        /// </summary>
        public int DaemonLogRetentionAmount { get; }
        /// <summary>
        /// How often to cleanup daemon logs based on the configured <see cref="DaemonLogRetentionMode"/> and <see cref="DaemonLogRetentionAmount"/>.
        /// </summary>
        public TimeSpan DaemonLogRetentionManagementInterval { get; }
    }

    /// <summary>
    /// Defines how backgrounds jobs are permanently deleted to free up storage and improve performance.
    /// </summary>
    public enum DeletionMode
    {
        /// <summary>
        /// Deletion daemon forwards the delete request to the <see cref="IStorage"/> which will handle the bulk deletion.
        /// Fastest option but does not provide a way to access the job state before deletion, only the ids of the deletes jobs will be available in an event.
        /// Will not raise <see cref="SystemDeletingBackgroundJobsEvent"/>.
        /// </summary>
        Bulk = 0,
        /// <summary>
        /// Delete daemon will query and delete jobs in bulk using <see cref="ILockedJob{TLockedJob, TChangeTracker, TState, TAction}.SystemDeleteAsync(IStorageConnection, string?, CancellationToken)"/>. 
        /// Slowest option but full job state is available in events. (Useful for example archiving the job in another environment)
        /// Should only be used when the system isn't under heavy load all the time so the daemon can keep up with the deletion.
        /// </summary>
        System = 1
    }

    /// <summary>
    /// Defines the retention method that should be used on colony daemon state.
    /// </summary>
    public enum ColonyDaemonRetentionMode
    {
        /// <summary>
        /// Nothing will be removed. Can cause storage to grow indefinitely.
        /// </summary>
        KeepAll = 0,
        /// <summary>
        /// Data will be removed based on a configured time value.
        /// </summary>
        OlderThan = 1,
        /// <summary>
        /// Data will be removed when the total size reaches a certain threshold. Data will be removed until the threshold is reached.
        /// </summary>
        Amount = 2
    }
}
