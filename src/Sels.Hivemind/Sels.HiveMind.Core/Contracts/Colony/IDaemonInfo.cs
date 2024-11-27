using Sels.Core.Extensions.Equality;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains the state of a daemon that is being managed by a <see cref="IColonyInfo"/>.
    /// </summary>
    public interface IDaemonInfo
    {
        // Properties
        /// <summary>
        /// The colony the daemon is running under.
        /// </summary>
        public IColonyInfo Colony { get; }
        /// <summary>
        /// The unique name of the daemon within the same colony.
        /// </summary>
        [Traceable(HiveLog.Daemon.Name)]
        public string Name { get; }
        /// <summary>
        /// The globally unique name of the daemon that includes the colony name.
        /// </summary>
        [Traceable(HiveLog.Daemon.Id)]
        public string FullName => $"{Colony.Id}.{Name}";
        /// <summary>
        /// The unique name of the daemon that includes the colony name and colony id.
        /// </summary>
        public string FullyQualifiedName => $"{Colony.Id}({Colony.Name}).{Name}";
        /// <summary>
        /// The start/stop priority of the daemon. Lower value means started earlier and stopped later.
        /// </summary>
        [Traceable(HiveLog.Daemon.Priority)]
        public byte Priority { get; }
        /// <summary>
        /// The type of the instance that the daemon can execute. Can be be null if the daemon is executing an anonymous delegate.
        /// </summary>
        public Type? InstanceType { get; }
        /// <summary>
        /// The current status of the daemon.
        /// </summary>
        [Traceable(HiveLog.Daemon.Status)]
        public DaemonStatus Status { get; }
        /// <summary>
        /// If the current daemon is waiting to start running.
        /// </summary>
        public bool IsPending => Status == DaemonStatus.Starting;
        /// <summary>
        /// If the current daemon is running.
        /// </summary>
        public bool IsRunning => Status == DaemonStatus.Running;
        /// <summary>
        /// If the current colony ran into an issue while trying to transition into another status.
        /// </summary>
        public bool IsFaulted => Status.In(DaemonStatus.FailedToStart, DaemonStatus.FailedToStop);
        /// <summary>
        /// If the current daemon ran successfully.
        /// </summary>
        public bool IsFinished => Status == DaemonStatus.Finished;
        /// <summary>
        /// If the current daemon timed out while waiting for it's task to stop.
        /// </summary>
        public bool IsTimedout => Status == DaemonStatus.Timedout;
        /// <summary>
        /// Optional state that a daemon can expose. Can be null if the daemon doesn't expose any state or the type isn't public.
        /// </summary>
        public object? State { get; }
        /// <summary>
        /// The restart policy for this daemon.
        /// </summary>
        public DaemonRestartPolicy RestartPolicy { get; }
        /// <summary>
        /// The log level above which to start persisted created logs.
        /// </summary>
        public LogLevel EnabledLogLevel { get; }

        /// <summary>
        /// The queryable properties of the daemon.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}
