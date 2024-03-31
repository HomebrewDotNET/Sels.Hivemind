using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Dictates how the update process should behave when updating a recurring job that is currently running.
    /// </summary>
    public enum RecurringJobUpdateBehaviour
    {
        /// <summary>
        /// Wait for the job to gracefully stop running before locking and updating the job.
        /// </summary>
        Wait = 0,
        /// <summary>
        /// Cancel the job and wait for it to gracefully stop running before locking and updating the job.
        /// </summary>
        Cancel = 1
    }
}
