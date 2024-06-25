using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using Sels.Core.Extensions.Text;
using Sels.Core.Extensions.Fluent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Sels.HiveMind.Storage.Job
{
    /// <summary>
    /// Contains all state related to a background job transformed into a format for storage.
    /// </summary>
    public class BackgroundJobStorageData : JobStorageData
    {
        /// <summary>
        /// Creates a new instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to convert from</param>
        /// <param name="invocationStorageData"><inheritdoc cref="InvocationData"/></param>
        /// <param name="lockStorageData"><inheritdoc cref="Lock"/></param>
        /// <param name="middleware"><inheritdoc cref="Middleware"/></param>
        /// <param name="properties"><inheritdoc cref="Properties"/></param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public BackgroundJobStorageData(IReadOnlyBackgroundJob job, InvocationStorageData invocationStorageData, LockStorageData lockStorageData, IEnumerable<StorageProperty> properties, IEnumerable<MiddlewareStorageData> middleware, HiveMindOptions options, IMemoryCache cache = null)
            : base(invocationStorageData, lockStorageData, properties, middleware, options, cache)
        {
            job.ValidateArgument(nameof(job));

            Id = job.Id;
            ExecutionId = job.ExecutionId;
            Queue = job.Queue;
            Priority = job.Priority;
            CreatedAtUtc = job.CreatedAtUtc;
            ModifiedAtUtc = job.ModifiedAtUtc;

            job.ChangeTracker.NewProperties.Execute(x => ChangeTracker.NewProperties.Add(Properties.First(p => p.Name.EqualsNoCase(x))));
            job.ChangeTracker.UpdatedProperties.Execute(x => ChangeTracker.UpdatedProperties.Add(Properties.First(p => p.Name.EqualsNoCase(x))));
            job.ChangeTracker.RemovedProperties.Execute(x => ChangeTracker.RemovedProperties.Add(x));
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BackgroundJobStorageData()
        {
            
        }
    }
}
