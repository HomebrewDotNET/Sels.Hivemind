﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a state that a job can be in.
    /// </summary>
    [Traceable(HiveLog.Job.State, nameof(Name))]
    public interface IJobState
    {
        /// <summary>
        /// The unique name of the state.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Represents a unique sequence number for the state. 
        /// Sequence is increased each time a job transitions into a new state.
        /// </summary>
        public long Sequence { get; set; }
        /// <summary>
        /// The date (in utc) when the state was elected for a background job.
        /// </summary>
        public DateTime ElectedDateUtc { get; set; }
        /// <summary>
        /// The date (machine time) when the state was elected for a background job.
        /// </summary>
        public DateTime ElectedDate => ElectedDateUtc.ToLocalTime();

        /// <summary>
        /// The reason why the job was transitioned into the current state.
        /// </summary>
        public string? Reason { get; set; }
    }
}
