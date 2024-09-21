using Sels.HiveMind.Colony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Service
{
    /// <summary>
    /// Service used for managing HiveMind colonies.
    /// </summary>
    public interface IColonyService
    {
        /// <summary>
        /// Tries to sync the current state of <paramref name="colony"/> to the storage if it can get the process lock on the colony.
        /// </summary>
        /// <param name="colony">The colony to sync and get a lock for</param>
        /// <param name="requester">Who is requesting the lock</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>WasLocked: If the lock on <paramref name="colony"/> was acquired|Lock: The current state of the lock on <paramref name="colony"/> regardless if the lock was acquired</returns>
        public Task<(bool WasLocked, ILockInfo Lock)> TrySyncAndGetProcessLockAsync(IColonyInfo colony, string requester, CancellationToken token = default);
        /// <summary>
        /// Tries to heartbeat the process lock on <paramref name="colony"/> by <paramref name="holder"/>.
        /// </summary>
        /// <param name="colony">The colony the heartbeat the lock on</param>
        /// <param name="holder">Who is supposed to have the process lock on <paramref name="colony"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>HeartbeatWasSet: If the heartbeat on <paramref name="colony"/> was set for <paramref name="holder"/>|Lock: The current state of the lock on <paramref name="colony"/> regardless if the heartbeat was set. Can be null if the colony was deleted</returns>
        public Task<(bool HeartbeatWasSet, ILockInfo Lock)> TryHeartbeatProcessLockAsync(IColonyInfo colony, [Traceable(HiveLog.Colony.Holder)] string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to persist the current state of <paramref name="colony"/>.
        /// </summary>
        /// <param name="colony">The colony whoes state to persist</param>
        /// <param name="newDaemonLogs">Dictionary with new logs to persist where the key is the name of the daemon</param>
        /// <param name="holder">Who is supposed to have the process lock on the colony</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>True if the state was persisted or false if <paramref name="holder"/> doesn't hold the lock anymore</returns>
        public Task<bool> TrySyncStateAsync(IColonyInfo colony, IReadOnlyDictionary<string, IEnumerable<LogEntry>> newDaemonLogs, [Traceable(HiveLog.Colony.Holder)]  string holder, CancellationToken token = default);
        /// <summary>
        /// Tries to release the process lock on <paramref name="colony"/> if it is still held by <paramref name="requester"/>. If the lock is still held by <paramref name="requester"/> the last state of the colony will also be synced.
        /// </summary>
        /// <param name="colony">The colony to release the lock from</param>
        /// <param name="newDaemonLogs">Dictionary with new logs to persist where the key is the name of the daemon</param>
        /// <param name="holder">Who is supposed to have the process lock on <paramref name="colony"/></param>
        /// <param name="token">Optional token to cancel the request</param>
        public Task ReleaseLockAndSyncStateIfHeldByAsync(IColonyInfo colony, IReadOnlyDictionary<string, IEnumerable<LogEntry>> newDaemonLogs, [Traceable(HiveLog.Colony.Holder)] string holder, CancellationToken token = default);
    }
}
