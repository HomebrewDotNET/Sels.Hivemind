using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <summary>
    /// Contains all state related to a job transformed into a format for storage.
    /// </summary>
    public class JobStorageData
    {
        // Properties
        /// <summary>
        /// Unique id regenerated each time a job is persisted with a new state.
        /// Used to correlate jobs placed in a queue and the state of the job to deal with rogue messages.
        /// </summary>
        public Guid ExecutionId { get; set; }
        /// <summary>
        /// The unique id of the job. Will be null during creation.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The name of the queue the job is placed in.
        /// </summary>
        public string Queue { get; set; }
        /// <summary>
        /// The priority of the job in <see cref="Queue"/>.
        /// </summary>
        public QueuePriority Priority { get; set; }
        /// <summary>
        /// The date (in utc) the job was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// The last date (in utc) the job was modified.
        /// </summary>
        public DateTime ModifiedAtUtc { get; set; }

        /// <summary>
        /// Data about how to execute the job transformed into a format for storage.
        /// </summary>
        public InvocationStorageData InvocationData { get; set; }

        /// <summary>
        /// The lock on the job transformed into a format for storage. Will be set null if a lock is released.
        /// </summary>
        public LockStorageData Lock { get; set; }

        /// <summary>
        /// The properties tied to the job transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<StorageProperty> Properties { get; set; }
        /// <summary>
        /// Any middleware to execute for the job transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<MiddlewareStorageData> Middleware { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="invocationStorageData"><inheritdoc cref="InvocationData"/></param>
        /// <param name="lockStorageData"><inheritdoc cref="Lock"/></param>
        /// <param name="middleware"><inheritdoc cref="Middleware"/></param>
        /// <param name="properties"><inheritdoc cref="Properties"/></param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public JobStorageData(InvocationStorageData invocationStorageData, LockStorageData lockStorageData, IEnumerable<StorageProperty> properties, IEnumerable<MiddlewareStorageData> middleware, HiveMindOptions options, IMemoryCache cache = null)
        {
            options.ValidateArgument(nameof(options));
            invocationStorageData.ValidateArgument(nameof(invocationStorageData));

            InvocationData = invocationStorageData;
            Lock = lockStorageData;
            Properties = properties != null ? properties.ToList() : new List<StorageProperty>();
            Middleware = middleware != null ? middleware.ToList() : new List<MiddlewareStorageData>();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public JobStorageData()
        {

        }
    }
}
