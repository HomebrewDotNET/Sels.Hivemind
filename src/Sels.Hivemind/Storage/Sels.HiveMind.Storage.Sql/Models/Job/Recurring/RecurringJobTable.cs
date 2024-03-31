using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions.DateTimes;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Job;
using Sels.HiveMind.Schedule;
using Sels.HiveMind.Models.Storage.Schedule;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to a table that contains recurring job state.
    /// </summary>
    public class RecurringJobTable : BaseJobTable<string>
    {
        // Properties
        /// <summary>
        /// <see cref="ISchedule"/> serialized to json.
        /// </summary>
        public string Schedule { get; set; }
        /// <summary>
        /// <see cref="IRecurringJobSettings"/> serialized to json.
        /// </summary>
        public string Settings { get; set; }
        /// <inheritdoc cref="IReadOnlyRecurringJob.ExpectedExecutionDateUtc"/>
        public DateTime? ExpectedExecutionDate { get; }
        /// <inheritdoc cref="IReadOnlyRecurringJob.ExecutedAmount"/>
        public long ExecutedAmount { get; }
        /// <inheritdoc cref="IReadOnlyRecurringJob.LastCompletedDateUtc"/>
        public DateTime? LastStartedDate { get; }
        /// <inheritdoc cref="IReadOnlyRecurringJob.LastCompletedDateUtc"/>
        public DateTime? LastCompletedDate { get; }

        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public RecurringJobTable(RecurringJobStorageData job, HiveMindOptions options, IMemoryCache? cache) : base(job, options, cache)
        {
            ExpectedExecutionDate = job.ExpectedExecutionDateUtc.HasValue ? job.ExpectedExecutionDateUtc.Value.ToUniversalTime() : (DateTime?)null;
            ExecutedAmount = job.ExecutedAmount;
            LastStartedDate = job.LastStartedDateUtc.HasValue ? job.LastStartedDateUtc.Value.ToUniversalTime() : (DateTime?)null;
            LastCompletedDate = job.LastCompletedDateUtc.HasValue ? job.LastCompletedDateUtc.Value.ToUniversalTime() : (DateTime?)null;
        }

        /// <summary>
        /// Creates an instance from <paramref name="configuration"/>.
        /// </summary>
        /// <param name="configuration">The instance to create from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public RecurringJobTable(RecurringJobConfigurationStorageData configuration, HiveMindOptions options, IMemoryCache? cache)
        {
            configuration.ValidateArgument(nameof(configuration));
            options.ValidateArgument(nameof(options));
            
            Id = configuration.Id;
            Queue = configuration.Queue;
            Priority = configuration.Priority;
            LockedBy = configuration.Requester;
            LockedAt = DateTime.UtcNow.ToUniversalTime();
            LockHeartbeat = DateTime.UtcNow.ToUniversalTime();
            Schedule = HiveMindHelper.Storage.ConvertToStorageFormat(configuration.Schedule, options, cache);
            Settings = HiveMindHelper.Storage.ConvertToStorageFormat(configuration.Settings, options, cache);
            InvocationData = HiveMindHelper.Storage.ConvertToStorageFormat(configuration.InvocationData, options, cache);
            if (configuration.Middleware.HasValue()) MiddlewareData = HiveMindHelper.Storage.ConvertToStorageFormat(configuration.Middleware, options, cache);
            CreatedAt = configuration.CreatedAt.ToUniversalTime();
            ModifiedAt = configuration.ModifedAt.ToUniversalTime();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public RecurringJobTable()
        {

        }

        /// <summary>
        /// Converts the current instance to it's storage format equivalent.
        /// </summary>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <returns>The current instance in it's storage format equivalent</returns>
        public RecurringJobStorageData ToStorageFormat(HiveMindOptions options, IMemoryCache cache)
        {
            options.ValidateArgument(nameof(options));

            return new RecurringJobStorageData()
            {
                Id = Id.ToString(),
                ExecutionId = ExecutionId.HasValue() ? new Guid(ExecutionId) : default,
                Queue = Queue,
                Priority = Priority,
                ExpectedExecutionDateUtc = ExpectedExecutionDate.HasValue ? ExpectedExecutionDate.Value.AsUtc() : (DateTime?)null,
                ExecutedAmount = ExecutedAmount,
                LastStartedDateUtc = LastStartedDate.HasValue ? LastStartedDate.Value.AsUtc() : (DateTime?)null,
                LastCompletedDateUtc = LastCompletedDate.HasValue ? LastCompletedDate.Value.AsUtc() : (DateTime?)null,
                CreatedAtUtc = CreatedAt.AsUtc(),
                ModifiedAtUtc = ModifiedAt.AsUtc(),
                Schedule = HiveMindHelper.Storage.ConvertFromStorageFormat(Schedule, typeof(ScheduleStorageData), options, cache).CastTo<ScheduleStorageData>(),
                Settings = HiveMindHelper.Storage.ConvertFromStorageFormat(Settings, typeof(RecurringJobSettings), options, cache).CastTo<RecurringJobSettings>(),
                InvocationData = HiveMindHelper.Storage.ConvertFromStorageFormat(InvocationData, typeof(InvocationStorageData), options, cache).CastTo<InvocationStorageData>(),
                Middleware = MiddlewareData.HasValue() ? HiveMindHelper.Storage.ConvertFromStorageFormat(MiddlewareData, typeof(List<MiddlewareStorageData>), options, cache).CastTo<List<MiddlewareStorageData>>() : null
            };
        }
    }
}
