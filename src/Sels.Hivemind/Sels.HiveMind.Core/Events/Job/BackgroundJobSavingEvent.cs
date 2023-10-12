using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when changes to a background job are being persisted.
    /// </summary>
    public class BackgroundJobSavingEvent
    {
        // Properties
        /// <summary>
        /// The job being saved.
        /// </summary>
        public IWriteableBackgroundJob Job { get; }
        /// <summary>
        /// True if the job is being created, otherwise false if being updated.
        /// </summary>
        public bool IsCreation { get; }
        /// <summary>
        /// The storage connection that is being used to save the job. Transaction is still active during event handling and can be rollbacked upon failure (if one was started in the first place).
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="BackgroundJobSavingEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="connection"><inheritdoc cref="StorageConnection"/></param>
        /// <param name="isCreation"><inheritdoc cref="IsCreation"/></param>
        public BackgroundJobSavingEvent(IWriteableBackgroundJob job, IStorageConnection connection, bool isCreation)
        {
            Job = job.ValidateArgument(nameof(job));
            StorageConnection = connection.ValidateArgument(nameof(connection));
            IsCreation = isCreation;
        }
    }
}
