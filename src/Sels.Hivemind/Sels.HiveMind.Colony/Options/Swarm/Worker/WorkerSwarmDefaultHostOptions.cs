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
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="WorkerSwarmHostOptions"/>
    /// </summary>
    public class WorkerSwarmDefaultHostOptions
    {
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.MaxJobCommitTime"/>.
        /// </summary>
        public TimeSpan MaxJobCommitTime { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.MaxNotFoundWaitTime"/>.
        /// </summary>
        public TimeSpan MaxNotFoundWaitTime { get; set; } = TimeSpan.FromMilliseconds(250);
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.NotFoundCheckInterval"/>.
        /// </summary>
        public int NotFoundCheckInterval { get; set; } = 2;
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.LockedDelay"/>.
        /// </summary>
        public TimeSpan LockedDelay { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.LockHeartbeatSafetyOffset"/>.
        /// </summary>
        public TimeSpan LockHeartbeatSafetyOffset { get; set; }
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.LogLevel"/>.
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Error;
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.LogFlushInterval"/>.
        /// </summary>
        public TimeSpan LogFlushInterval { get; set; } = TimeSpan.FromSeconds(2);
    }

    /// <summary>
    /// Contains the validation rules <see cref="WorkerSwarmDefaultHostOptions"/>
    /// </summary>
    public class WorkerSwarmDefaultHostOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="WorkerSwarmDefaultHostOptionsValidationProfile"/>
        public WorkerSwarmDefaultHostOptionsValidationProfile() : base()
        {
            CreateValidationFor<WorkerSwarmDefaultHostOptions>()
                .ForProperty(x => x.NotFoundCheckInterval)
                    .MustBeLargerOrEqualTo(1);
        }
    }
}
