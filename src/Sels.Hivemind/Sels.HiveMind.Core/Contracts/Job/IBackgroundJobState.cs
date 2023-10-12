using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Represents a state that a <see cref="IReadOnlyBackgroundJob"/> can be in.
    /// </summary>
    public interface IBackgroundJobState
    {
        /// <summary>
        /// The unique name of the state.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The date (in utc) when the state was elected for a background job.
        /// </summary>
        public DateTime ElectedDateUtc { get; }
        /// <summary>
        /// The date (machine time) when the state was elected for a background job.
        /// </summary>
        public DateTime ElectedDate => ElectedDateUtc.ToLocalTime();

        /// <summary>
        /// The reason why the job was transitioned into the current state.
        /// </summary>
        public string Reason { get; }
    }
}
