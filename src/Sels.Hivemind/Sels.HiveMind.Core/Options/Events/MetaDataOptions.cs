using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Exposes extra options for persisting meta data for jobs.
    /// </summary>
    public class JobMetaDataOptions
    {
        /// <summary>
        /// The meta data to persist for jobs.
        /// </summary>
        public JobMetaData MetaData { get; set; } = JobMetaData.All;
    }

    [Flags]
    public enum JobMetaData
    {
        /// <summary>
        /// No meta data to persist.
        /// </summary>
        None = 0,

        /// <summary>
        /// Persist everything.
        /// </summary>
        All = Machine | User | Thread,
        /// <summary>
        /// Persist properties related to the source machine that created the job.
        /// </summary>
        Machine = MachineName | MachinePlatform | MachineArchitecture,
        /// <summary>
        /// Persist properties related to the user of the process that created the job.
        /// </summary>
        User = UserName | UserDomain,
        /// <summary>
        /// Persist properties related to the culture of the thread that persisted the properties.
        /// </summary>
        Thread = ThreadCulture | ThreadUiCulture,

        /// <inheritdoc cref="HiveMindConstants.Job.Properties.SourceMachineName"/>
        MachineName = 1,
        /// <inheritdoc cref="HiveMindConstants.Job.Properties.SourceMachinePlatform"/>
        MachinePlatform = 2,
        /// <inheritdoc cref="HiveMindConstants.Job.Properties.SourceMachineArchitecture"/>
        MachineArchitecture = 4,
        /// <inheritdoc cref="HiveMindConstants.Job.Properties.SourceUserName"/>
        UserName = 8,
        /// <inheritdoc cref="HiveMindConstants.Job.Properties.SourceUserDomain"/>
        UserDomain = 16,
        /// <inheritdoc cref="HiveMindConstants.Job.Properties.ThreadCulture"/>
        ThreadCulture = 32,
        /// <inheritdoc cref="HiveMindConstants.Job.Properties.ThreadUiCulture"/>
        ThreadUiCulture = 64
    }
}
