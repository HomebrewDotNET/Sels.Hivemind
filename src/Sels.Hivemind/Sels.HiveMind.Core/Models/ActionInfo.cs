using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Action that is scheduled to be executed on a running 
    /// </summary>
    public class ActionInfo 
    {
        /// <summary>
        /// The unique id assigned to the action by the storage.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The id of the component the action is scheduled for.
        /// </summary>
        public string ComponentId { get; set; }
        /// <summary>
        /// The type of the action to execute.
        /// </summary>
        public Type Type { get; set; }
        /// <summary>
        /// Optional context for the action.
        /// </summary>
        public object Context { get; set; }
        /// <summary>
        /// The execution id of the component when the action was scheduled. The id's have to match for the action to be executed, otherwise the action will be removed.
        /// </summary>
        public Guid ExecutionId { get; set; }
        /// <summary>
        /// Can be used to ignore <see cref="ExecutionId"/> and just execute the action regardless of the execution id.
        /// </summary>
        public bool ForceExecute { get; set; }  
        /// <summary>
        /// The priority of the action to determine the execution order. Lower values means the action will be executed sooner.
        /// </summary>
        public byte Priority { get; set; } = byte.MaxValue;
        /// <summary>
        /// When the action was created (in utc).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// When the action was created (local machine).
        /// </summary>
        public DateTime CreatedAt => CreatedAtUtc.ToLocalTime();
    }
}
