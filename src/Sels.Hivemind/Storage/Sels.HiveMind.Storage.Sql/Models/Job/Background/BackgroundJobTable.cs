using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.Core.Extensions;
using System.Xml.Linq;
using Sels.Core.Extensions.Conversion;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.Core.Extensions.DateTimes;
using Dapper;
using System.Data;
using Sels.HiveMind.Storage.Job.Background;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to a table that contains background job state.
    /// </summary>
    public class BackgroundJobTable : BaseJobTable<long>
    {
        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public BackgroundJobTable(BackgroundJobStorageData job, HiveMindOptions options, IMemoryCache? cache) : base(job, options, cache)
        {
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
        public BackgroundJobStorageData ToStorageFormat(HiveMindOptions options, IMemoryCache cache)
        {
            options.ValidateArgument(nameof(options));

            return new BackgroundJobStorageData()
            {
                Id = Id.ToString(),
                ExecutionId = new Guid(ExecutionId),
                Queue = Queue,
                Priority = Priority,
                CreatedAtUtc = CreatedAt.AsUtc(),
                ModifiedAtUtc = ModifiedAt.AsUtc(),
                InvocationData = HiveMindHelper.Storage.ConvertFromStorageFormat(InvocationData, typeof(InvocationStorageData), options, cache).CastTo<InvocationStorageData>(),
                Middleware = MiddlewareData.HasValue() ? HiveMindHelper.Storage.ConvertFromStorageFormat(MiddlewareData, typeof(List<MiddlewareStorageData>), options, cache).CastTo<List<MiddlewareStorageData>>() : null
            };
        }

        /// <inheritdoc/>
        public override DynamicParameters ToUpdateParameters(string holder, bool releaseLock)
        {
            holder.ValidateArgument(nameof(holder));

            var parameters = base.ToUpdateParameters(holder, releaseLock);
            parameters.AddBackgroundJobId(Id, nameof(Id));

            return parameters;
        }
    }
}
