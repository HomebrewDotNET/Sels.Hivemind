using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage.Job;
using Sels.Core.Extensions;
using System.Xml.Linq;
using Sels.Core.Extensions.Conversion;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Sels.HiveMind.Storage.Sql.Job
{
    /// <summary>
    /// Models that maps to a table that contains background job state.
    /// </summary>
    public class BackgroundJobTable : BaseLockableTable
    {
        /// <inheritdoc cref="IReadOnlyBackgroundJob.ExecutionId"/>
        public string? ExecutionId { get; set; }
        /// <inheritdoc cref="IReadOnlyBackgroundJob.Queue"/>
        public string? Queue { get; set; }
        /// <inheritdoc cref="IReadOnlyBackgroundJob.Priority"/>
        public QueuePriority Priority { get; set; }

        /// <summary>
        /// <see cref="IInvocationInfo"/> serialized to json.
        /// </summary>
        public string? InvocationData { get; set; }
        /// <summary>
        /// Collection of <see cref="IMiddlewareInfo"/> serialized to json.
        /// </summary>
        public string? MiddlewareData { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public BackgroundJobTable(JobStorageData job, HiveMindOptions options, IMemoryCache? cache) : base(job)
        {
            job.ValidateArgument(nameof(job));
            options.ValidateArgument(nameof(options));

            ExecutionId = job.ExecutionId.ToString();
            Queue = job.Queue;
            Priority = job.Priority;
            InvocationData = HiveMindHelper.Storage.ConvertToStorageFormat(job.InvocationData, options, cache);
            if(job.Middleware.HasValue()) MiddlewareData = HiveMindHelper.Storage.ConvertToStorageFormat(job.Middleware, options, cache);
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BackgroundJobTable()
        {

        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public JobStorageData ToStorageFormat(HiveMindOptions options, IMemoryCache cache)
        {
            options.ValidateArgument(nameof(options));

            return new JobStorageData()
            {
                Id = Id.ToString(),
                ExecutionId = new Guid(ExecutionId),
                Queue = Queue,
                Priority = Priority,
                CreatedAtUtc = CreatedAt,
                ModifiedAtUtc = ModifiedAt,
                InvocationData = HiveMindHelper.Storage.ConvertFromStorageFormat(InvocationData, typeof(InvocationStorageData), options, cache).CastTo<InvocationStorageData>(),
                Middleware = MiddlewareData.HasValue() ? HiveMindHelper.Storage.ConvertFromStorageFormat(InvocationData, typeof(List<MiddlewareStorageData>), options, cache).CastTo<List<MiddlewareStorageData>>() : null
            };
        }
    }
}
