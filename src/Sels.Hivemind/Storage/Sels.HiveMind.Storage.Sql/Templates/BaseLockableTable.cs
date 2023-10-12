using Sels.Core.Extensions;
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
        public string? LockedBy { get; }
        /// <summary>
        /// The date the lock was placed.
        /// </summary>
        public DateTime? LockedAt { get; }
        /// <summary>
        /// The last date that <see cref="LockedBy"/> extended the lock.
        /// </summary>
        public DateTime? LockHeartbeat { get; }

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
                LockedAt = job.Lock.LockedAtUtc;
                LockHeartbeat = job.Lock.LockHeartbeatUtc;
            }
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseLockableTable()
        {

        }
    }
}
