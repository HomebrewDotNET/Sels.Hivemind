using System;
using System.Collections.Generic;
using System.Linq;
using Sels.Core.Extensions;
using Sels.HiveMind.Job;

namespace Sels.HiveMind.Storage.Job
{
    /// <summary>
    /// A job state transformed into a format for storage.
    /// </summary>
    public class JobStateStorageData
    {
        /// <summary>
        /// The original type name of the state.
        /// </summary>
        public string OriginalTypeName { get; set; }
        /// <summary>
        /// The unique name of the state.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The date (in utc) when the state was elected for a background job.
        /// </summary>
        public DateTime ElectedDateUtc { get; set; }

        /// <summary>
        /// The reason why the job was transitioned into the current state.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The properties of the state transformed into a format for storage.
        /// </summary>
        public List<StorageProperty> Properties { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The instance to convert from</param>
        /// <param name="properties">Any queryable properties on <paramref name="state"/></param>
        public JobStateStorageData(IJobState state, IEnumerable<StorageProperty> properties)
        {
            state.ValidateArgument(nameof(state));
            OriginalTypeName = state.GetType().AssemblyQualifiedName;
            Name = state.Name;
            ElectedDateUtc = state.ElectedDateUtc;
            Reason = state.Reason;

            Properties = properties?.ToList();
        }

        /// <summary>
        /// Creates anew instance.
        /// </summary>
        public JobStateStorageData()
        {
            
        }
    }
}
