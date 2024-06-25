using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Linq;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Models.Storage.Schedule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Storage.Job
{
    /// <summary>
    /// Contains all state related to a recurring job transformed into a format for storage.
    /// </summary>
    public class RecurringJobStorageData : JobStorageData
    {
        /// <summary>
        /// The schedule that will be used to determine the next time the job needs to be executed transformed into a format for storage.
        /// </summary>
        public ScheduleStorageData Schedule { get; set; }

        /// <summary>
        /// The settings assigned to the recurring job.
        /// </summary>
        public RecurringJobSettings Settings { get; set; }

        /// <summary>
        /// The expected time (in utc) after which the recurring job should be executed.
        /// </summary>
        public DateTime? ExpectedExecutionDateUtc { get; set; }
        /// <summary>
        /// How many times the recurring job has been executed.
        /// </summary>
        public long ExecutedAmount { get; set; }
        /// <summary>
        /// The last time (in utc) that the execution of the recurring job was started.
        /// </summary>
        public DateTime? LastStartedDateUtc { get; set; }
        /// <summary>
        /// The last time (in utc) that the execution of the recurring job was completed.
        /// </summary>
        public DateTime? LastCompletedDateUtc { get; set; }

        /// <summary>
        /// Creates a new instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to convert into a format for storage</param>
        /// <param name="recurringJobScheduleStorageData"><inheritdoc cref="Schedule"/></param>
        /// <param name="invocationStorageData"><inheritdoc cref="InvocationData"/></param>
        /// <param name="lockStorageData"><inheritdoc cref="Lock"/></param>
        /// <param name="middleware"><inheritdoc cref="Middleware"/></param>
        /// <param name="properties"><inheritdoc cref="Properties"/></param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public RecurringJobStorageData(IReadOnlyRecurringJob job, RecurringJobSettings settings, ScheduleStorageData recurringJobScheduleStorageData, InvocationStorageData invocationStorageData, LockStorageData lockStorageData, IEnumerable<StorageProperty> properties, IEnumerable<MiddlewareStorageData> middleware, HiveMindOptions options, IMemoryCache cache = null)
            : base(invocationStorageData, lockStorageData, properties, middleware, options, cache)
        {
            job.ValidateArgument(nameof(job));
            Settings = settings.ValidateArgument(nameof(settings));

            Id = job.Id;
            ExecutionId = job.ExecutionId;
            Queue = job.Queue;
            Priority = job.Priority;
            CreatedAtUtc = job.CreatedAtUtc;
            ModifiedAtUtc = job.ModifiedAtUtc;
            Schedule = recurringJobScheduleStorageData.ValidateArgument(nameof(recurringJobScheduleStorageData));
            ExpectedExecutionDateUtc = job.ExpectedExecutionDateUtc;
            ExecutedAmount = job.ExecutedAmount;
            LastStartedDateUtc = job.LastStartedDateUtc;
            LastCompletedDateUtc = job.LastCompletedDateUtc;

            job.ChangeTracker.NewProperties.Execute(x => ChangeTracker.NewProperties.Add(Properties.First(p => p.Name.EqualsNoCase(x))));
            job.ChangeTracker.UpdatedProperties.Execute(x => ChangeTracker.UpdatedProperties.Add(Properties.First(p => p.Name.EqualsNoCase(x))));
            job.ChangeTracker.RemovedProperties.Execute(x => ChangeTracker.RemovedProperties.Add(x));
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public RecurringJobStorageData()
        {

        }
    }
}
