using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <summary>
    /// Contains all state related to a recurring job transformed into a format for storage.
    /// </summary>
    public class RecurringJobStateStorageData : JobStateStorageData
    {
        /// <summary>
        /// Represents a unique sequence number for the state. 
        /// Sequence is increased each time a rcurring job transitions into a new state.
        /// </summary>
        public long Sequence { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The instance to convert from</param>
        /// <param name="properties">Any queryable properties on <paramref name="state"/></param>
        public RecurringJobStateStorageData(IRecurringJobState state, IEnumerable<StorageProperty> properties) : base(state, properties)
        {
            state.ValidateArgument(nameof(state));
            Sequence = state.Sequence;
        }

        /// <summary>
        /// Creates anew instance.
        /// </summary>
        public RecurringJobStateStorageData()
        {

        }
    }
}
