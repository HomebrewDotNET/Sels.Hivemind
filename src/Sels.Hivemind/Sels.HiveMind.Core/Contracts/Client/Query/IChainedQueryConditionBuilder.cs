using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for joining together multiple condition.
    /// </summary>
    /// <typeparam name="T">The type to return for the fluent syntax</typeparam>
    public interface IChainedQueryConditionBuilder<T>
    {
        /// <summary>
        /// The current condition and the one created after this call both need to return true
        /// </summary>
        T And { get; }
        /// <summary>
        /// The current condition or the one created after this call either need to return true
        /// </summary>
        T Or { get; }
    }
}
