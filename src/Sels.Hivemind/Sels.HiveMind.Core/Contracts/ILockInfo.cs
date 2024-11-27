using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains the state of a lock on a component.
    /// </summary>
    public interface ILockInfo
    {
        /// <summary>
        /// The process that currently has the lock.
        /// </summary>
        [Traceable(HiveLog.Job.LockHolder)]
        public string LockedBy { get; }
        /// <summary>
        /// The date (in utc) when the lock on the job was acquired by <see cref="LockedBy"/>.
        /// </summary>
        public DateTime LockedAtUtc { get; }
        /// <summary>
        /// The date (machine time) when the lock on the job was acquired by <see cref="LockedBy"/>.
        /// </summary>
        public DateTime LockedAt => LockedAtUtc.ToLocalTime();
        /// <summary>
        /// The last date (in utc) that <see cref="LockedBy"/> extended the lock.
        /// </summary>
        public DateTime LockHeartbeatUtc { get; }
        /// <summary>
        /// The last date (machine time) that <see cref="LockedBy"/> extended the lock.
        /// </summary>
        public DateTime LockHeartbeat => LockHeartbeatUtc.ToLocalTime();
    }
}
