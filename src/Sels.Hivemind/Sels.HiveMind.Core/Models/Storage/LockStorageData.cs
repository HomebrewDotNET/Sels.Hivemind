using Sels.Core.Extensions;
using Sels.Core.Extensions.DateTimes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// The state of a lock on a component transformed into a format for storage.
    /// </summary>
    public class LockStorageData : ILockInfo
    {
        /// <inheritdoc cref="ILockInfo.LockedBy"/>
        public string LockedBy { get; set; }
        /// <inheritdoc cref="ILockInfo.LockedAtUtc"/>
        public DateTime LockedAtUtc { get; set; }
        /// <inheritdoc cref="ILockInfo.LockHeartbeatUtc"/>
        public DateTime LockHeartbeatUtc { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="lockInfo"/>.
        /// </summary>
        /// <param name="lockInfo">The instance to convert from</param>
        public LockStorageData(ILockInfo lockInfo)
        {
            lockInfo.ValidateArgument(nameof(lockInfo));
            LockedBy = lockInfo.LockedBy;
            LockedAtUtc = lockInfo.LockedAtUtc;
            LockHeartbeatUtc = lockInfo.LockHeartbeatUtc;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public LockStorageData()
        {
            
        }

        /// <summary>
        /// Sets the the kind of the dates of the current instance to utc.
        /// </summary>
        /// <returns>Current instance</returns>
        public LockStorageData ToUtc()
        {
            LockHeartbeatUtc = LockHeartbeatUtc.AsUtc();
            LockedAtUtc = LockedAtUtc.AsUtc();
            return this;
        }
    }
}
