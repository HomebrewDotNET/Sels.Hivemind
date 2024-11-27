using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue.Sql
{
    /// <summary>
    /// Contains the names of the tables used by HiveMind sql queues for a certain environment because they don't support schema's.
    /// </summary>
    public class QueueTableNames
    {
        // Fields
        private readonly string _environment;

        // Properties
        /// <summary>
        /// The name of the generic job queue table.
        /// </summary>
        public string GenericJobQueueTable => $"HiveMind.{_environment}.Queue.Generic";
        /// <summary>
        /// The name of the queue table that just contains the background jobs to process.
        /// </summary>
        public string BackgroundJobProcessQueueTable => $"HiveMind.{_environment}.Queue.BackgroundJobProcessing";
        /// <summary>
        /// The name of the queue table that just contains the recurring jobs to process.
        /// </summary>
        public string RecurringJobProcessQueueTable => $"HiveMind.{_environment}.Queue.RecurringJobProcessing";

        /// <inheritdoc cref="QueueTableNames"/>
        /// <param name="environment">The HiveMind environment to get the table names for</param>
        public QueueTableNames(string environment)
        {
            HiveMindHelper.Validation.ValidateEnvironment(environment);
            _environment = environment;
        }
    }
}
