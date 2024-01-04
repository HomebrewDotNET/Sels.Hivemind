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

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Worker
{
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="WorkerSwarmHostOptions"/>
    /// </summary>
    public class WorkerSwarmDefaultHostOptions : BackgroundJobSwarmHostDefaultOptions
    {

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
        }
    }
}
