﻿using System;
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
    public class JobStorageData
    {
        // Fields
        private List<JobStateStorageData> _states;

        // Properties
        /// <summary>
        /// The unique id of the background job. Will be null during creation.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The name of the queue the job is placed in.
        /// </summary>
        public string Queue { get; set; }
        /// <inheritdoc cref="IReadOnlyBackgroundJob.ExecutionId"/>.
        public Guid ExecutionId { get; set; }
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
        /// The states of the job transformed into a format for storage. Last state is always the current state of the job.
        /// </summary>
        public IReadOnlyList<JobStateStorageData> States { get => _states; 
            set 
            {
                _states = value?.ToList();
                ChangeTracker.NewStates.Clear();
            } 
        }
        /// <summary>
        /// The properties tied to the job transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<StorageProperty> Properties { get; set; }
        /// <summary>
        /// Any middleware to execute for the job transformed into a format for storage.
        /// </summary>
        public IReadOnlyList<MiddlewareStorageData> Middleware { get; set; }

        /// <inheritdoc cref="IBackgroundJobChangesTracker"/>
        public JobStorageChangeTracker ChangeTracker { get; } = new JobStorageChangeTracker();

        /// <summary>
        /// Creates a new instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to convert from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public JobStorageData(IReadOnlyBackgroundJob job, InvocationStorageData invocationStorageData, LockStorageData lockStorageData, IEnumerable<StorageProperty> properties, IEnumerable<MiddlewareStorageData> middleware, HiveMindOptions options, IMemoryCache cache = null)
        {
            job.ValidateArgument(nameof(job));
            options.ValidateArgument(nameof(options));
            invocationStorageData.ValidateArgument(nameof(invocationStorageData));

            Id = job.Id;
            ExecutionId = job.ExecutionId;
            Queue = job.Queue;
            Priority = job.Priority;
            CreatedAtUtc = job.CreatedAtUtc;
            ModifiedAtUtc = job.ModifiedAtUtc;

            InvocationData = invocationStorageData;
            Lock = lockStorageData;
            Properties = properties != null ? properties.ToList() : new List<StorageProperty>();
            Middleware = middleware != null ? middleware.ToList() : new List<MiddlewareStorageData>();

            job.ChangeTracker.NewProperties.Execute(x => ChangeTracker.NewProperties.Add(Properties.First(p => p.Name.EqualsNoCase(x))));
            job.ChangeTracker.UpdatedProperties.Execute(x => ChangeTracker.UpdatedProperties.Add(Properties.First(p => p.Name.EqualsNoCase(x))));
            job.ChangeTracker.RemovedProperties.Execute(x => ChangeTracker.RemovedProperties.Add(x));
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public JobStorageData()
        {
            
        }

        /// <summary>
        /// Adds a new state to store.
        /// </summary>
        /// <param name="state">The state to store</param>
        /// <param name="isNew">Indicates if <paramref name="state"/> is new and needs to be persisted</param>
        public void AddState(JobStateStorageData state, bool isNew)
        {
            state.ValidateArgument(nameof(state));

            _states ??= new List<JobStateStorageData>();
            _states.Add(state);
            if(isNew) ChangeTracker.NewStates.Add(state);
        }
    }
}
