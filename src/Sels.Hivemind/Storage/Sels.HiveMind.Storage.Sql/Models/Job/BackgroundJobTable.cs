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
        public BackgroundJobTable(JobStorageData job) : base(job)
        {
            job.ValidateArgument(nameof(job));

            ExecutionId = job.ExecutionId.ToString();
            Queue = job.Queue;
            Priority = job.Priority;
            InvocationData = HiveMindHelper.Storage.ConvertToStorageFormat(job.InvocationData);
            if(job.Middleware.HasValue()) MiddlewareData = HiveMindHelper.Storage.ConvertToStorageFormat(job.Middleware);
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
        /// <returns>The current instance in it's storage format equivalent</returns>
        public JobStorageData ToStorageFormat() => new JobStorageData()
        {
            Id = Id.ToString(),
            ExecutionId = new Guid(ExecutionId),
            Queue = Queue,
            Priority = Priority,
            CreatedAtUtc = CreatedAt,
            ModifiedAtUtc = ModifiedAt,
            InvocationData = HiveMindHelper.Storage.ConvertFromStorageFormat(InvocationData, typeof(InvocationStorageData)).CastTo<InvocationStorageData>(),
            Middleware = MiddlewareData.HasValue() ? HiveMindHelper.Storage.ConvertFromStorageFormat(InvocationData, typeof(List<MiddlewareStorageData>)).CastTo<List<MiddlewareStorageData>>() : null
        };
    }
}
