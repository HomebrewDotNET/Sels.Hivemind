using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Colony.Swarm;
using Sels.HiveMind.Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Contains the current state of a swarm.
    /// </summary>
    /// <typeparam name="TOptions">The type of options used by the swarm</typeparam>
    public class WorkerSwarmState<TOptions> : ISwarmState<TOptions>
    {
        /// <inheritdoc/>
        public TOptions Options { get; internal set; }
        /// <inheritdoc/>
        public string Name { get; internal set; }
        /// <inheritdoc/>
        public IReadOnlyList<IDroneState<TOptions>>? Drones { get; internal set; }
        /// <inheritdoc/>
        [JsonIgnore]
        public ISwarmState<TOptions> Parent { get; internal set; }
        /// <inheritdoc/>
        public IReadOnlyList<ISwarmState<TOptions>> ChildSwarms { get; internal set; }
        /// <inheritdoc/>
        [JsonIgnore]
        public IJobScheduler Scheduler { get; internal set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            var builder = new StringBuilder();
            ToString(builder);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the current state to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The builder to append to</param>
        /// <param name="currentIndent">The current currentIndent of the builder</param>
        public void ToString(StringBuilder builder, int currentIndent = 0)
        {
            builder.ValidateArgument(nameof(builder));
            currentIndent.ValidateArgumentLargerOrEqual(nameof(currentIndent), 0);

            // Append swarm header
            builder.Append('\t', currentIndent).Append('[').Append(Name).Append("]").Append(':').Append($"Processed={this.CastTo<ISwarmState<TOptions>>().Processed}");
            if (ChildSwarms.HasValue())
            {
                // Count total processed by all childs
                var childProcessed = ChildSwarms.Sum(x => x.Processed);
                builder.Append("=>").Append(childProcessed).Append('(').Append(childProcessed + this.CastTo<ISwarmState<TOptions>>().Processed).Append(')').AppendLine();

                // Append child swarms
                currentIndent++;
                for (int i = 0; i < ChildSwarms.Count; i++)
                {
                    var childSwarm = ChildSwarms[i];
                    if (childSwarm is WorkerSwarmState<TOptions> swarmState)
                    {
                        swarmState.ToString(builder, currentIndent);
                        if (i < ChildSwarms.Count - 1)
                        {
                            builder.AppendLine();
                        }
                    }
                }
                currentIndent--;
            }
            // Append drone state
            if (Drones.HasValue())
            {
                builder.AppendLine();
                foreach (var (droneState, i) in Drones!.Select((x, i) => (x, i)))
                {
                    var isProcessing = droneState.IsProcessing;
                    builder.Append('\t', currentIndent).Append(' ', 2).Append(droneState.Name).Append('(').Append(isProcessing ? "ACTIVE" : "IDLE").Append(")");
                    if (isProcessing)
                    {
                        builder.Append(':').Append($"Job={droneState.JobId}|Queue={droneState.JobQueue}|Priority={droneState.JobPriority}|Duration={(droneState.Duration?.TotalMilliseconds ?? 0)}ms|DurationStats(Last/Min/Avg/Max)={droneState.LastDuration?.TotalMilliseconds ?? 0}/{droneState.MinDuration?.TotalMilliseconds ?? 0}/{droneState.AvgDuration?.TotalMilliseconds ?? 0}/{droneState.MaxDuration?.TotalMilliseconds ?? 0}ms|WaitStats(Last/Min/Avg/Max)={droneState.LastWait?.TotalMilliseconds ?? 0}/{droneState.MinWait?.TotalMilliseconds ?? 0}/{droneState.AvgWait?.TotalMilliseconds ?? 0}/{droneState.MaxWait?.TotalMilliseconds ?? 0}ms|Processed={droneState.Processed}");
                    }
                    else
                    {
                        builder.Append(':').Append($"DurationStats(Last/Min/Avg/Max)={droneState.LastDuration?.TotalMilliseconds ?? 0}/{droneState.MinDuration?.TotalMilliseconds ?? 0}/{droneState.AvgDuration?.TotalMilliseconds ?? 0}/{droneState.MaxDuration?.TotalMilliseconds ?? 0}ms|WaitStats(Last/Min/Avg/Max)={droneState.LastWait?.TotalMilliseconds ?? 0}/{droneState.MinWait?.TotalMilliseconds ?? 0}/{droneState.AvgWait?.TotalMilliseconds ?? 0}/{droneState.MaxWait?.TotalMilliseconds ?? 0}ms|Processed={droneState.Processed}");
                    }

                    if (i < Drones!.Count - 1)
                    {
                        builder.AppendLine();
                    }
                }
            }
        }
    }
}
