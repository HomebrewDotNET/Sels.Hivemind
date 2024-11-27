using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Base class for creating a <see cref="IJobState"/>.
    /// </summary>
    /// <typeparam name="T">The type inheriting from the current class</typeparam>
    public abstract class BaseJobState<T> : IJobState
    {
        /// <inheritdoc cref="Name"/>
        [JsonIgnore]
        public static string StateName => typeof(T).Name.Contains("State") ? typeof(T).Name.Replace("State", string.Empty) : typeof(T).Name;

        /// <inheritdoc/>
        [JsonIgnore]
        public virtual string Name => StateName;
        /// <inheritdoc/>
        [JsonIgnore]
        public long Sequence { get; set; }
        /// <inheritdoc/>
        [JsonIgnore]
        public DateTime ElectedDateUtc { get; set; }

        /// <inheritdoc/>
        [JsonIgnore]
        public virtual string? Reason { get; set; }
    }
}
