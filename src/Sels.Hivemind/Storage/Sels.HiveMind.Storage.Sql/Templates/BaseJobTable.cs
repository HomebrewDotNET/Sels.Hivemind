using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class for tables that contain the state of jobs.
    /// </summary>
    /// <typeparam name="T">The type of the primary key used by the job</typeparam>
    public class BaseJobTable<T> : BaseLockableTable<T>
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
        public BaseJobTable(JobStorageData job, HiveMindOptions options, IMemoryCache? cache) : base(job)
        {
            job.ValidateArgument(nameof(job));
            options.ValidateArgument(nameof(options));

            ExecutionId = job.ExecutionId.ToString();
            Queue = job.Queue;
            Priority = job.Priority;
            InvocationData = HiveMindHelper.Storage.ConvertToStorageFormat(job.InvocationData, options, cache);
            if (job.Middleware.HasValue()) MiddlewareData = HiveMindHelper.Storage.ConvertToStorageFormat(job.Middleware, options, cache);
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseJobTable()
        {

        }
    }
}
