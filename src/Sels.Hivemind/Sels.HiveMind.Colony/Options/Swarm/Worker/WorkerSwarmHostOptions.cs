using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.Worker
{
    /// <inheritdoc cref="IWorkerSwarmHostOptions"/>
    public class WorkerSwarmHostOptions : SwarmHostOptions<WorkerSwarmHostOptions>, IWorkerSwarmHostOptions
    {
        // Properties
        /// <inheritdoc cref="IWorkerSwarmHostOptions.Middleware"/>
        public List<MiddlewareStorageData> Middelware { get; set; }
        /// <inheritdoc/>
        IReadOnlyCollection<MiddlewareStorageData> IWorkerSwarmHostOptions.Middleware => Middelware;
        /// <inheritdoc/>
        public bool UseMiddlewareFromParentSwarms { get; set; } = true;
        /// <inheritdoc/>
        public TimeSpan? MaxJobCommitTime { get; set; }
        /// <inheritdoc/>
        public TimeSpan? MaxNotFoundWaitTime { get; set; }
        /// <inheritdoc/>
        public int? NotFoundCheckInterval { get; set; }
        /// <inheritdoc/>
        public TimeSpan? LockedDelay { get; set; }
        /// <inheritdoc/>
        public TimeSpan? LockHeartbeatSafetyOffset { get; set; }
        /// <inheritdoc/>
        public LogLevel? LogLevel { get; set; }

        /// <inheritdoc cref="WorkerSwarmHostOptions"/>
        public WorkerSwarmHostOptions() : base()
        {

        }

        /// <inheritdoc cref="WorkerSwarmHostOptions"/>
        /// <param name="name"><inheritdoc cref="Name"/></param>
        /// <param name="configurator">Delegate to configure this instance</param>
        public WorkerSwarmHostOptions(string name, Action<WorkerSwarmHostOptions> configurator)
        {
            Name = name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            configurator.ValidateArgument(nameof(configurator))(this);
        }

        /// <inheritdoc/>
        protected override WorkerSwarmHostOptions CreateSubSwarmOptions(string name, Action<WorkerSwarmHostOptions> builder)
        {
            return new WorkerSwarmHostOptions(name, builder);
        }
    }

    /// <summary>
    /// Contains the validation rules <see cref="WorkerSwarmHostOptions"/>
    /// </summary>
    public class WorkerSwarmHostOptionsValidationProfile : SwarmHostOptionsValidationProfile<WorkerSwarmHostOptions>
    {
        /// <inheritdoc cref="WorkerSwarmHostOptionsValidationProfile"/>
        public WorkerSwarmHostOptionsValidationProfile() : base()
        {
            CreateValidationFor<WorkerSwarmHostOptions>()
                .ForProperty(x => x.NotFoundCheckInterval, x => x.Value)
                    .MustBeLargerOrEqualTo(1);

            ImportFrom<SharedValidationProfile>();
        }
    }
}
