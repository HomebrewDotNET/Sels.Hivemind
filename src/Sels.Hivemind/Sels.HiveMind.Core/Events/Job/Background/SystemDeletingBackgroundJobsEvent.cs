using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Events.Job.Background
{
    /// <summary>
    /// Events that is raised when a batch of background jobs is being moved to the <see cref="SystemDeletingState"/>.
    /// Only triggered by the deletion daemon.
    /// </summary>
    public class SystemDeletingBackgroundJobsEvent
    {
        /// <summary>
        /// The background jobs being deleted.
        /// </summary>
        public ILockedBackgroundJob[] BackgroundJobs { get; }

        /// <inheritdoc cref="SystemDeletingBackgroundJobsEvent"/>
        /// <param name="jobs"><inheritdoc cref="ILockedBackgroundJob"/></param>
        public SystemDeletingBackgroundJobsEvent(IEnumerable<ILockedBackgroundJob> jobs)
        {
            BackgroundJobs = jobs.ValidateArgument(nameof(jobs)).ToArray();
        }
    }
}
