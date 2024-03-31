using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job
{
    /// <summary>
    /// Raised when a recurring job was either created or updated.
    /// </summary>
    public class RecurringJobPersistedEvent
    {
        /// <summary>
        /// The job that was created or updated.
        /// </summary>
        public IReadOnlyRecurringJob Job { get; }
        /// <summary>
        /// True if <see cref="Job"/> was created, otherwise false if updated.
        /// </summary>
        public bool WasCreation { get; }

        /// <inheritdoc cref="RecurringJobPersistedEvent"/>
        /// <param name="job"><inheritdoc cref="Job"/></param>
        /// <param name="wasCreation"><inheritdoc cref="WasCreation"/></param>
        public RecurringJobPersistedEvent(IReadOnlyRecurringJob job, bool wasCreation)
        {
            Job = job.ValidateArgument(nameof(job));
            WasCreation = wasCreation;
        }
    }
}
