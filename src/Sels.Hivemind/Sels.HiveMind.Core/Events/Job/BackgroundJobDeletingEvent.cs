using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a background job is being deleted.
    /// </summary>
    public class BackgroundJobDeletingEvent
    {
        // Properties
        /// <summary>
        /// The job being deleted.
        /// </summary>
        public IWriteableBackgroundJob Job { get; }
        /// <summary>
        /// The storage connection that is being used to delete the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IClientConnection Connection { get; }

        /// <inheritdoc cref="BackgroundJobDeletingEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        public BackgroundJobDeletingEvent(IWriteableBackgroundJob job, IClientConnection connection)
        {
            Job = job.ValidateArgument(nameof(job));
            Connection = connection.ValidateArgument(nameof(connection));
        }
    }
}
