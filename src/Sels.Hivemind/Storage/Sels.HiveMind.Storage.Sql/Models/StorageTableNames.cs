using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql
{
    /// <summary>
    /// Contains the names of the tables used by HiveMind sql storages for a certain environment because they don't support schema's.
    /// </summary>
    public class StorageTableNames
    {
        // Fields
        private readonly string _environment;

        // Properties
        /// <summary>
        /// The name of the table that contains the background jobs.
        /// </summary>
        public string BackgroundJobTable => $"HiveMind.{_environment}.BackgroundJob";
        /// <summary>
        /// The name of the table that contains the background job properties.
        /// </summary>
        public string BackgroundJobPropertyTable => $"HiveMind.{_environment}.BackgroundJobProperty";
        /// <summary>
        /// The name of the table that contains the background job states.
        /// </summary>
        public string BackgroundJobStateTable => $"HiveMind.{_environment}.BackgroundJobState";
        /// <summary>
        /// The name of the table that contains the background job state properties.
        /// </summary>
        public string BackgroundJobStatePropertyTable => $"HiveMind.{_environment}.BackgroundJobStateProperty";
        /// <summary>
        /// The name of the table that contains the background job processing logs.
        /// </summary>
        public string BackgroundJobLogTable => $"HiveMind.{_environment}.BackgroundJobLog";
        /// <summary>
        /// The name of the table that contains the processing data assigned to a job.
        /// </summary>
        public string BackgroundJobDataTable => $"HiveMind.{_environment}.BackgroundJobData";
        /// <summary>
        /// The name of the table that contains the pending actions to execute on executing background jobs.
        /// </summary>
        public string BackgroundJobActionTable => $"HiveMind.{_environment}.BackgroundJobAction";

        /// <summary>
        /// The name of the table that contains the recurring jobs.
        /// </summary>
        public string RecurringJobTable => $"HiveMind.{_environment}.RecurringJob";
        /// <summary>
        /// The name of the table that contains the recurring job properties.
        /// </summary>
        public string RecurringJobPropertyTable => $"HiveMind.{_environment}.RecurringJobProperty";
        /// <summary>
        /// The name of the table that contains the recurring job states.
        /// </summary>
        public string RecurringJobStateTable => $"HiveMind.{_environment}.RecurringJobState";
        /// <summary>
        /// The name of the table that contains the recurring job state properties.
        /// </summary>
        public string RecurringJobStatePropertyTable => $"HiveMind.{_environment}.RecurringJobStateProperty";
        /// <summary>
        /// The name of the table that contains the recurring job processing logs.
        /// </summary>
        public string RecurringJobLogTable => $"HiveMind.{_environment}.RecurringJobLog";
        /// <summary>
        /// The name of the table that contains the processing data assigned to a job.
        /// </summary>
        public string RecurringJobDataTable => $"HiveMind.{_environment}.RecurringJobData";
        /// <summary>
        /// The name of the table that contains the pending actions to execute on executing recurring jobs.
        /// </summary>
        public string RecurringJobActionTable => $"HiveMind.{_environment}.RecurringJobAction";
        /// <summary>
        /// The name of the table that contains the current state of a colony connected to an environment.
        /// </summary>
        public string ColonyTable => $"HiveMind.{_environment}.Colony";
        /// <summary>
        /// The name of the table that contains the properties of a colony.
        /// </summary>
        public string ColonyPropertyTable => $"HiveMind.{_environment}.ColonyProperty";
        /// <summary>
        /// The name of the table that contains the daemons managed by a colony.
        /// </summary>
        public string ColonyDaemonTable => $"HiveMind.{_environment}.ColonyDaemon";
        /// <summary>
        /// The name of the table that contains the properties of a daemon.
        /// </summary>
        public string ColonyDaemonPropertyTable => $"HiveMind.{_environment}.ColonyDaemonProperty";
        /// <summary>
        /// The name of the table that contains the logs created by a daemon.
        /// </summary>
        public string ColonyDaemonLogTable => $"HiveMind.{_environment}.ColonyDaemonLog";

        /// <inheritdoc cref="StorageTableNames"/>
        /// <param name="environment">The HiveMind environment to get the table names for</param>
        public StorageTableNames(string environment)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);
            _environment = environment;
        }
    }
}
