using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Data;
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

        /// <summary>
        /// Creates dapper parameters to insert the current instance.
        /// </summary>
        /// <returns>Dapper parameters to insert the current instance</returns>
        public virtual DynamicParameters ToCreateParameters()
        {
            var parameters = new DynamicParameters();
            parameters.Add(nameof(ExecutionId), ExecutionId, DbType.String, ParameterDirection.Input, 36);
            parameters.Add(nameof(Queue), Queue, DbType.String, ParameterDirection.Input, 255);
            parameters.Add(nameof(Priority), Priority, DbType.Int32, ParameterDirection.Input);
            parameters.Add(nameof(InvocationData), InvocationData, DbType.String, ParameterDirection.Input, 16777215);
            parameters.Add(nameof(MiddlewareData), MiddlewareData, DbType.String, ParameterDirection.Input, 16777215);
            parameters.Add(nameof(CreatedAt), CreatedAt, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add(nameof(ModifiedAt), ModifiedAt, DbType.DateTime2, ParameterDirection.Input);

            return parameters;
        }

        /// <summary>
        /// Creates dapper parameters to update the current instance.
        /// </summary>
        /// <param name="holder">Who is supposed to hold the lock on the job</param>
        /// <param name="releaseLock">If the lock by <paramref name="holder"/> should be kept after the update</param>
        /// <returns>Dapper parameters to update the current instance</returns>
        public virtual DynamicParameters ToUpdateParameters(string holder, bool releaseLock)
        {
            holder.ValidateArgument(nameof(holder));

            var parameters = new DynamicParameters();
            parameters.Add(nameof(ExecutionId), ExecutionId, DbType.String, ParameterDirection.Input, 36);
            parameters.Add(nameof(Queue), Queue, DbType.String, ParameterDirection.Input, 255);
            parameters.Add(nameof(Priority), Priority, DbType.Int32, ParameterDirection.Input);
            parameters.Add(nameof(ModifiedAt), ModifiedAt, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add(nameof(LockedBy), !releaseLock ? LockedBy : null, DbType.String, ParameterDirection.Input, 100);
            parameters.Add(nameof(LockedAt), !releaseLock ? LockedAt : (DateTime?)null, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add(nameof(LockHeartbeat), !releaseLock ? LockHeartbeat : (DateTime?)null, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add(nameof(releaseLock), releaseLock, DbType.Boolean, ParameterDirection.Input);
            parameters.AddLocker(holder, nameof(holder));

            return parameters;
        }
    }
}
