using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Recurring
{
    /// <summary>
    /// Raised a recurring job is being updated.
    /// </summary>
    public class RecurringJobUpdatingEvent
    {
        // Properties
        /// <summary>
        /// The recurring job being updated.
        /// </summary>
        public IWriteableRecurringJob Job { get; }
        /// <summary>
        /// The storage connection that is being used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="RecurringJobUpdatingEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        public RecurringJobUpdatingEvent(IWriteableRecurringJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
           Connection = connection.ValidateArgument(nameof(connection));
        }
    }
}
