using Sels.Core.Extensions.Equality;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains the state of a colony that is connected to a HiveMind environment.
    /// </summary>
    public interface IColonyInfo
    {
        // Properties
        /// <summary>
        /// The unique id of the colony. Must be globally unique as this id is used to get a process lock.
        /// </summary>
        [Traceable(HiveLog.Colony.Id)]
        public string Id { get; }
        /// <summary>
        /// The display name of the colony. When not set defaults to <see cref="Id"/>.
        /// </summary>
        [Traceable(HiveLog.Colony.Name)]
        public string Name { get; }
        /// <summary>
        /// The HiveMind environment that the colony is connected to.
        /// </summary>
        [Traceable(HiveLog.Environment)]
        public string Environment { get; }
        /// <summary>
        /// The current state of the lock on the colony. Can be null if not locked.
        /// </summary>
        public ILockInfo? Lock { get; }
        /// <summary>
        /// The current status of the colony.
        /// </summary>
        [Traceable(HiveLog.Colony.Status)]
        public ColonyStatus Status { get; }
        /// <summary>
        /// If the current colony is waiting to start running.
        /// </summary>
        public bool IsPending => Status.In(ColonyStatus.Starting, ColonyStatus.WaitingForLock);
        /// <summary>
        /// If the current colony is running.
        /// </summary>
        public bool IsRunning => Status == ColonyStatus.Running;
        /// <summary>
        /// If the current colony ran into an issue while trying to transition into another status.
        /// </summary>
        public bool IsFaulted => Status.In(ColonyStatus.FailedToStart, ColonyStatus.FailedToStop, ColonyStatus.FailedToLock, ColonyStatus.FailedToStartDaemons);
        /// <summary>
        /// The configured options for this instance.
        /// </summary>
        public IColonyOptions Options { get; }
        /// <summary>
        /// The daemons managed by the colony.
        /// </summary>
        public IReadOnlyList<IDaemonInfo> Daemons { get; }
        /// <summary>
        /// The queryable properties of the colony.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}
