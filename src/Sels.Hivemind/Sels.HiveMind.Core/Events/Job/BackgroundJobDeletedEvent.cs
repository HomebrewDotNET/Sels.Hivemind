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
    /// Raised when a background job was deleted.
    /// </summary>
    public class BackgroundJobDeletedEvent
    {
        // Properties
        /// <summary>
        /// The job that was deleted.
        /// </summary>
        public IReadOnlyBackgroundJob Job { get; }
        /// <summary>
        /// The client connection that was used to delete the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="BackgroundJobDeletedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        public BackgroundJobDeletedEvent(IReadOnlyBackgroundJob job, IStorageConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            Connection = connection.ValidateArgument(nameof(connection));
        }
    }
}
