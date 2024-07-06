using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.DistributedLocking
{
    /// <summary>
    /// An active distributed lock on a global resource.
    /// </summary>
    public interface IDistributedLock : ILockable, IAsyncDisposable
    {
        /// <summary>
        /// The resource the lock is placed on.
        /// </summary>
        string Resource { get; }
        /// <summary>
        /// The environment the lock is placed in.
        /// </summary>
        string Environment { get; }
        /// <summary>
        /// The process who is holding the lock.
        /// </summary>
        string Holder { get; }
    }
}
