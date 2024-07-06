using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// Represents an object that can be locked.
    /// </summary>
    public interface ILockable
    {
        /// <summary>
        /// The expected time (in utc) after which the lock on the current objects becomes invalid.
        /// Should only be implemented when <see cref="IsSelfManaged"/> is false.
        /// </summary>
        public DateTime ExpectedTimeoutUtc { get; }
        /// <summary>
        /// The expected time (local machine) after which the lock on the current object becomes invalid.
        /// Should only be implemented when <see cref="IsSelfManaged"/> is false.
        /// </summary>
        public DateTime ExpectedTimeout => ExpectedTimeoutUtc.ToLocalTime();
        /// <summary>
        /// True if the object manages the locks itself, false if the lock needs to be managed manually using <see cref="TryKeepAliveAsync(CancellationToken)"/>.
        /// </summary>
        public bool IsSelfManaged { get; }

        /// <summary>
        /// Tries to keep the current dequeued job alive. Needs to be called to ensure dequeued job stays locked during processing.
        /// Should only be implemented when <see cref="IsSelfManaged"/> is false.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the dequeued job is still valid, otherwise false</returns>
        public Task<bool> TryKeepAliveAsync(CancellationToken token = default);
        /// <summary>
        /// Registers <paramref name="action"/> that should be called when the lock on the job becomes expired.
        /// Should only be only be implemented when <see cref="IsSelfManaged"/> is true.
        /// </summary>
        /// <param name="action">The delegate to call</param>
        public void OnLockExpired(Func<Task> action);

    }
}
