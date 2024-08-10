using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Scheduler
{
    /// <summary>
    /// Contains the options for a <see cref="DistributedLockJobSchedulerMiddleware"/>.
    /// </summary>
    public class DistributedLockJobSchedulerMiddlewareOptions
    {
        /// <summary>
        /// The name of the lock that should be acquired before returning a <see cref="IDequeuedJob"/>.
        /// When set to null the queue name will be used as the lock name.
        /// </summary>
        public string? LockName { get; set; }
        /// <summary>
        /// Optional prefix to add before <see cref="LockName"/>. Only usefull when <see cref="LockName"/> is not set.
        /// </summary>
        public string? LockNamePrefix { get; set; }
        /// <summary>
        /// How many concurrent locks on <see cref="LockName"/> should be allowed. Lock name will be suffixed with a number from 1 to <see cref="MaxConcurrency"/>.
        /// </summary>
        public int MaxConcurrency { get; set; } = 1;
        /// <summary>
        /// By default the acquiring of locks is done in a random to avoid multiple processes trying to lock the same lock at the same time.
        /// The random order makes is more likelt for a process to pick a free lock.
        /// When set to true the first lock that will be tried will always be 1 and then increased until <see cref="MaxConcurrency"/>.
        /// </summary>
        public bool AlwaysLockInOrder { get; set; }
        /// <summary>
        /// How long to wait to acquire a lock before giving up.
        /// Null means no timeout so the middleware will wait indefinitely. This also implies that <see cref="NotAcquiredBehaviour"/> will not be used.
        /// Can be set to <see cref="TimeSpan.Zero"/> to try locking without any waiting.
        /// </summary>
        public TimeSpan? Timeout { get; set; }
        /// <summary>
        /// What the middlware should do when all locks are in use for a job.
        /// </summary>
        public DistributedLockNotAcquiredBehaviour NotAcquiredBehaviour { get; set; } = DistributedLockNotAcquiredBehaviour.ReturnAndDisableQueue;
        /// <summary>
        /// The maximum amount of time the middleware can sleep depending on the <see cref="NotAcquiredBehaviour"/> configured. Will be used as delay when disabling a queue as well.
        /// </summary>
        public TimeSpan DelayTime { get; set; } = TimeSpan.FromSeconds(15);
    }
    /// <summary>
    /// Determines the behaviour when a <see cref="DistributedLockJobSchedulerMiddleware"/> could not acquire a lock.
    /// </summary>
    public enum DistributedLockNotAcquiredBehaviour
    {
        /// <summary>
        /// Job will be returned to the queue and the middleware will sleep before trying again.
        /// </summary>
        ReturnAndSleep = 0,
        /// <summary>
        /// Job will be kept in memory and the middleware will sleep before trying to lock the job again.
        /// </summary>
        KeepAndSleep = 1,
        /// <summary>
        /// Job will be returned to the queue and the middleware will disable the queue the job came from for a set amount of time before trying again.
        /// When a global lock name is used all queues in the current group will be disabled.
        /// </summary>
        ReturnAndDisableQueue = 2,
        /// <summary>
        /// Job will be returned to the queue and the middleware will remove the queue the job came from before trying again.
        /// Queue will need to be re-added manually or by another middleware.
        /// When a global lock name is used all queues in the current group will be removed.
        /// </summary>
        ReturnAndRemoveQueue = 3
    }

    /// <summary>
    /// Contains the validation rules for <see cref="DistributedLockJobSchedulerMiddlewareOptions"/>.
    /// </summary>
    public class DistributedLockJobSchedulerMiddlewareOptionsValidationProfile : ValidationProfile<string>
    {
        // Statics
        /// <summary>
        /// The instance of the validation profile.
        /// </summary>
        public static DistributedLockJobSchedulerMiddlewareOptionsValidationProfile Instance { get; } = new DistributedLockJobSchedulerMiddlewareOptionsValidationProfile();

        /// <inheritdoc cref="DistributedLockJobSchedulerMiddlewareOptionsValidationProfile"/>
        private DistributedLockJobSchedulerMiddlewareOptionsValidationProfile()
        {
            CreateValidationFor<DistributedLockJobSchedulerMiddlewareOptions>()
                .ForProperty(x => x.MaxConcurrency)
                    .MustBeLargerThan(0)
                .ForProperty(x => x.LockName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.LockNamePrefix)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace();
        }
    }
}
