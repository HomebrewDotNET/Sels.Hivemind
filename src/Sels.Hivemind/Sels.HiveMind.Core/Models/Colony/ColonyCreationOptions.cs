namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Determines the creation options for a colony.
    /// </summary>
    [Flags]
    public enum ColonyCreationOptions
    {
        /// <summary>
        /// No options selected.
        /// </summary>
        None = 0,
        /// <summary>
        /// The default options.
        /// </summary>
        Default = AutoCreateLockMonitor | AutoCreateDeletionDaemon | AutoCreateRecurringJobWorkerSwarmHost,
        /// <summary>
        /// Enables the creation of <see cref="LockMonitorDaemon"/>.
        /// </summary>
        AutoCreateLockMonitor = 1,
        /// <summary>
        /// Enables the creation of an auto managed <see cref="DeletionDaemon"/>.
        /// </summary>
        AutoCreateDeletionDaemon = 2,
        /// <summary>
        /// Enables the creation of an auto managed <see cref="RecurringJobWorkerSwarmHost"/>.
        /// </summary>
        AutoCreateRecurringJobWorkerSwarmHost = 4
    }
}
