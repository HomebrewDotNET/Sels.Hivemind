using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Contains the current state of a drone in a swarm.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the swarm</typeparam>
    public class WorkerSwarmDroneState<TOptions> : IDroneState<TOptions>
    {
        // Fields
        private readonly Stopwatch _stopwatch = new Stopwatch();

        // State
        private TimeSpan? _lastDuration;

        // Properties
        /// <inheritdoc/>
        [JsonIgnore]
        public ISwarmState<TOptions> Swarm { get; internal set; }
        /// <inheritdoc/>
        public string Alias { get; internal set; }
        /// <inheritdoc/>
        public string Id { get; internal set; }
        /// <inheritdoc/>
        public bool IsProcessing { get; internal set; }
        /// <inheritdoc/>
        public bool IsWorkingOnDedicated { get; internal set; }

        /// <inheritdoc/>
        public string? JobId { get; internal set; }
        /// <inheritdoc/>
        public string? JobQueue { get; internal set; }
        /// <inheritdoc/>
        public QueuePriority JobPriority { get; internal set; } = QueuePriority.None;
        /// <inheritdoc/>
        public TimeSpan? Duration => IsProcessing ? _stopwatch.Elapsed : (TimeSpan?)null;
        /// <inheritdoc/>
        public TimeSpan? LastDuration => _lastDuration;
        /// <inheritdoc/>
        public TimeSpan? LastWait { get; internal set; }
        /// <inheritdoc/>
        public long Processed { get; internal set; }
        /// <inheritdoc/>
        public TimeSpan? MinDuration { get; internal set; }
        /// <inheritdoc/>
        public TimeSpan? MaxDuration { get; internal set; }
        /// <inheritdoc/>
        public TimeSpan? AvgDuration { get; internal set; }
        /// <inheritdoc/>
        public TimeSpan? MinWait { get; internal set; }
        /// <inheritdoc/>
        public TimeSpan? MaxWait { get; internal set; }
        /// <inheritdoc/>
        public TimeSpan? AvgWait { get; internal set; }

        /// <summary>
        /// Sets the state to that the drone is processing <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The job the drone is processing</param>
        /// <param name="lastWait"><inheritdoc cref="LastWait"/></param>
        public void SetProcessing(IDequeuedJob job, TimeSpan lastWait)
        {
            job.ValidateArgument(nameof(job));
            LastWait = lastWait;
            JobId = job.JobId;
            JobQueue = job.Queue;
            JobPriority = job.Priority;

            if (!MinWait.HasValue || lastWait < MinWait.Value) MinWait = lastWait;
            if (!MaxWait.HasValue || lastWait > MaxWait.Value) MaxWait = lastWait;
            if (AvgWait.HasValue)
            {
                AvgWait = AvgWait + ((lastWait - AvgWait) / (Processed + 1));
            }
            else
            {
                AvgWait = lastWait;
            }
            _stopwatch.Restart();
        }

        /// <summary>
        /// Sets the state that the drone is idle.
        /// </summary>
        public void SetIdle()
        {
            JobId = null;
            JobQueue = null;
            JobPriority = QueuePriority.None;
            _stopwatch.Stop();
            _lastDuration = _stopwatch.Elapsed;

            if (!MinDuration.HasValue || _lastDuration < MinDuration.Value) MinDuration = _lastDuration;
            if (!MaxDuration.HasValue || _lastDuration > MaxDuration.Value) MaxDuration = _lastDuration;
            if (AvgDuration.HasValue)
            {
                AvgDuration = AvgDuration + ((_lastDuration - AvgDuration) / (Processed + 1));
            }
            else
            {
                AvgDuration = _lastDuration;
            }
        }
    }
}
