using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Job.State
{
    /// <summary>
    /// Base class for creating a <see cref="IBackgroundJobState"/>.
    /// </summary>
    /// <typeparam name="T">The type inheriting from the current class</typeparam>
    public abstract class BaseBackgroundJobState<T> : IBackgroundJobState
    {
        /// <inheritdoc cref="Name"/>
        public static string StateName => typeof(T).Name.Contains("State") ? typeof(T).Name.Replace("State", string.Empty) : typeof(T).Name;    

        /// <inheritdoc/>
        public virtual string Name => StateName;
        /// <inheritdoc/>
        public DateTime ElectedDateUtc { get; set; }

        /// <inheritdoc/>
        public virtual string Reason { get; set; }
    }
}
