using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Background
{
    /// <summary>
    /// Raised when changes to a background job were persisted.
    /// </summary>
    public class BackgroundJobSavedEvent
    {
        // Properties
        /// <summary>
        /// The job that was saved.
        /// </summary>
        public IReadOnlyBackgroundJob Job { get; }
        /// <summary>
        /// True if the job was created, otherwise false if was updated.
        /// </summary>
        public bool IsCreation { get; }
        /// <summary>
        /// The client connection that was used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="BackgroundJobSavingEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="Connection"/></param>
        /// <param name="isCreation"><inheritdoc cref="IsCreation"/></param>
        public BackgroundJobSavedEvent(IReadOnlyBackgroundJob job, IStorageConnection connection, bool isCreation)
        {
            Job = job.ValidateArgument(nameof(job));
            Connection = connection.ValidateArgument(nameof(connection));
            IsCreation = isCreation;
        }
    }
}
