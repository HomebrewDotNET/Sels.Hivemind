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

namespace Sels.HiveMind.Colony.Swarm.Job
{
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="WorkerSwarmHostOptions{TMiddleware, TOptions}"/>
    /// </summary>
    public class WorkerSwarmDefaultHostOptions : JobSwarmHostDefaultOptions
    {
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.LogLevel"/>.
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Error;
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.LogFlushInterval"/>.
        /// </summary>
        public TimeSpan LogFlushInterval { get; set; } = TimeSpan.FromSeconds(2);
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.ActionPollingInterval/>
        /// </summary>
        public TimeSpan ActionPollingInterval { get; } = TimeSpan.FromSeconds(2);
        /// <summary>
        /// The default value for <see cref="WorkerSwarmHostOptions.ActionFetchLimit/>
        /// </summary>
        public int ActionFetchLimit { get; set; } = 10;
    }

    /// <summary>
    /// Contains the validation rules <see cref="WorkerSwarmDefaultHostOptions"/>
    /// </summary>
    public class WorkerSwarmDefaultHostOptionsValidationProfile : JobSwarmHostDefaultOptionsValidationProfile
    {
        /// <inheritdoc cref="WorkerSwarmDefaultHostOptionsValidationProfile"/>
        public WorkerSwarmDefaultHostOptionsValidationProfile() : base()
        {
            CreateValidationFor<WorkerSwarmDefaultHostOptions>()
                .ForProperty(x => x.ActionFetchLimit)
                    .MustBeLargerOrEqualTo(1)
                    .MustBeSmallerOrEqualTo(HiveMindConstants.Query.MaxDequeueLimit);
        }
    }
}
