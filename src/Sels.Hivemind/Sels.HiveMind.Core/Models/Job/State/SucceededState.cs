﻿using Sels.HiveMind.Job.State;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// State for background jobs that successfully executed.
    /// </summary>
    public class SucceededState : BaseBackgroundJobState<SucceededState>
    {
        // Properties
        /// <summary>
        /// How long the job took to execute.
        /// </summary>
        public TimeSpan Duration { get; }
        /// <summary>
        /// The total overhead of executing the job.
        /// Includes resolving the background job instance to invoke, generating the invocation delegete, resolving and executing the middleware, ...
        /// Does not include <see cref="Duration"/>.
        /// </summary>
        public TimeSpan Overhead { get; }
        /// <summary>
        /// The total duration of executing the job.
        /// </summary>
        public TimeSpan TotalDuration => Duration + Overhead;
        /// <summary>
        /// How long it took to execute the job starting from when the job was created until it successfully executed.
        /// </summary>
        public TimeSpan LeadTime { get; }
        /// <summary>
        /// The result returned by the executed job if a result was returned.
        /// </summary>
        public object Result { get; }

        /// <inheritdoc cref="SucceededState"/>
        /// <param name="duration"><inheritdoc cref="Duration"/></param>
        /// <param name="overhead"><inheritdoc cref="Overhead"/></param>
        /// <param name="leadTime"><inheritdoc cref="LeadTime"/></param>
        /// <param name="result"><inheritdoc cref="Result"/></param>
        public SucceededState(TimeSpan duration, TimeSpan overhead, TimeSpan leadTime, object result)
        {
            Duration = duration;
            Overhead = overhead;
            LeadTime = leadTime;
            Result = result;
        }
    }
}
