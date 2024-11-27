using Sels.Core.Async.TaskManagement;
using Sels.Core.Models.Disposables;
using Sels.Core.Scope;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Sels.HiveMind.Extensions
{
    /// <summary>
    /// Contains static extension methods for <see cref="ILockable"/>.
    /// </summary>
    public static class ILockableExtensions
    {
        /// <summary>
        /// Keeps the lock on <paramref name="lockable"/> alive during the scope of the returned <see cref="IDisposable"/>.
        /// </summary>
        /// <param name="lockable">The object whoes lock should be kept alive</param>
        /// <param name="requester">Who is requesting <paramref name="lockable"/> to be kept alive. Used as owner of the task if <paramref name="taskManager"/> is used</param>
        /// <param name="keepAliveTaskName">The name of the keepalive task to use if one is started using <paramref name="taskManager"/></param>
        /// <param name="taskManager">The task manager to use to start any keep alive task</param>
        /// <param name="safetyOffset">How lock before <paramref name="lockable"/> expires to try and heartbeat the lock</param>
        /// <param name="onExpiredAction">Delegate that will be called when the lock on <paramref name="lockable"/> expires</param>
        /// <param name="logger">Optional logger to use for tracing</param>
        /// <param name="token">Optional token that can be used to cancel the task</param>
        /// <returns>An object that is used to define the scope. Disposing it stops the scope</returns>
        public static IAsyncDisposable KeepAliveDuringScope(this ILockable lockable, object requester, string keepAliveTaskName, ITaskManager taskManager, TimeSpan safetyOffset, Func<Task> onExpiredAction, ILogger? logger = null, CancellationToken token = default)
        {
            lockable = Guard.IsNotNull(lockable);
            requester = Guard.IsNotNull(requester);
            keepAliveTaskName = Guard.IsNotNullOrWhitespace(keepAliveTaskName);
            taskManager = Guard.IsNotNull(taskManager);
            onExpiredAction = Guard.IsNotNull(onExpiredAction);

            if(lockable.IsExpired)
            {
                throw new InvalidOperationException($"Cannot keep lock alive on <{lockable}>. Lock is already expired");
            }

            if(lockable.SupportsExpiryNotification) lockable.OnLockExpired(onExpiredAction);

            // Self managed so no need to do anything
            if (lockable.IsSelfManaged)
            {
                return NullDisposer.Instance;
            }

            var pendingTask = taskManager.ScheduleDelayed(lockable.ExpectedTimeout.Add(-safetyOffset), (m, t) =>
            {
                return m.ScheduleActionAsync(requester, keepAliveTaskName, false, async t =>
                {
                    logger.Log($"Keep alive task for <{lockable}>> started");

                    while (!t.IsCancellationRequested)
                    {
                        var setTime = lockable.ExpectedTimeout.Add(-safetyOffset);
                        logger.Debug($"Keeping lock on <{lockable}> alive at <{setTime}>");
                        await Helper.Async.SleepUntil(setTime, t).ConfigureAwait(false);
                        if (t.IsCancellationRequested) return;

                        try
                        {
                            if (lockable.IsExpired)
                            {
                                logger.Warning($"Lock expired on <{lockable}> while sleeping. Stopping");
                                break;
                            }

                            logger.Debug($"Lock on <{lockable}> is about to expire or already expired. Trying to heartbeat to see if we still have lock");
                            if (!await lockable.TryKeepAliveAsync(t).ConfigureAwait(false))
                            {
                                logger.Warning($"Lock on <{lockable}> expired");
                                await onExpiredAction().ConfigureAwait(false);
                                break;
                            }
                            else
                            {
                                logger.Log($"Kept lock on <{lockable}> alive");
                            }
                        }
                        catch (OperationCanceledException) when (t.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.Log($"Could not keep lock on <{lockable}> alive", ex);
                            await onExpiredAction().ConfigureAwait(false);
                            break;
                        }
                    }
                    logger.Log( $"Keep alive task for <{lockable}> stopped");
                }, x => x.WithManagedOptions(ManagedTaskOptions.GracefulCancellation)
                         .WithPolicy(NamedManagedTaskPolicy.CancelAndStart)
                         .WithCreationOptions(TaskCreationOptions.PreferFairness)
                , token);
            });

            return new AsyncScopedAction(() => Task.CompletedTask, () =>
            {
                return pendingTask.CancelAndWaitOnFinalization();
            });
        }
    }
}
