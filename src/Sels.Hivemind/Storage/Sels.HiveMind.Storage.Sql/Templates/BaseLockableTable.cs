using Sels.Core.Extensions;
using Sels.Core.Extensions.DateTimes;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class for table where entities can be locked.
    /// </summary>
    public class BaseLockableTable : BaseIdTable
    {
        /// <summary>
        /// The process that currently has the lock.
        /// </summary>
        public string? LockedBy { get; set; }
        /// <summary>
        /// The date the lock was placed.
        /// </summary>
        public DateTime? LockedAt { get; set; }
        /// <summary>
        /// The last date that <see cref="LockedBy"/> extended the lock.
        /// </summary>
        public DateTime? LockHeartbeat { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        public BaseLockableTable(JobStorageData job) : base(job)
        {
            job.ValidateArgument(nameof(job));

            if(job.Lock != null)
            {
                LockedBy = job.Lock.LockedBy;
                LockedAt = job.Lock.LockedAtUtc.ToUniversalTime();
                LockHeartbeat = job.Lock.LockHeartbeatUtc.ToUniversalTime();
            }
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseLockableTable()
        {

        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public LockStorageData? ToLockStorageFormat()
        {
            if (LockedBy.HasValue() && LockedAt.HasValue && LockHeartbeat.HasValue)
            {
                return new LockStorageData()
                {
                    LockedBy = LockedBy,
                    LockedAtUtc = LockedAt.Value.AsUtc(),
                    LockHeartbeatUtc = LockHeartbeat.Value.AsUtc()
                };
            }
            return null;
        }
    }
}
