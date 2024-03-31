using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a recurring job was updated.
    /// </summary>
    public class RecurringJobUpdatedEvent
    {
        // Properties
        /// <summary>
        /// The recurring job that was updated.
        /// </summary>
        public IReadOnlyRecurringJob Job { get; }
        /// <summary>
        /// The connection that was used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="RecurringJobUpdatedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        public RecurringJobUpdatedEvent(IReadOnlyRecurringJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            Connection = connection.ValidateArgument(nameof(connection));
        }
    }
}
