using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Colony.Swarm.BackgroundJob.Deletion;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Deletion
{
    /// <inheritdoc cref="IDeletionDaemonOptions"/>
    public class DeletionDeamonOptions : BackgroundJobSwarmHostOptions<DeletionDeamonOptions>, IDeletionDaemonOptions
    {
        /// <inheritdoc/>
        public TimeSpan? ErrorDelay { get; set; }
        /// <inheritdoc/>
        public TimeSpan? AllowedDeviation { get; set; }
        /// <inheritdoc/>
        public bool IsAutoManaged { get; set; }
        /// <inheritdoc/>
        public double? AutoManagedDroneCoreMultiplier { get; set; }
        /// <inheritdoc/>
        public TimeSpan? AutoManagedRestartInterval { get; set; }

        /// <inheritdoc/>
        protected override DeletionDeamonOptions CreateSubSwarmOptions(string name, Action<DeletionDeamonOptions> builder)
        {
            var options = new DeletionDeamonOptions();
            options.Name = name;
            builder(options);
            return options;
        }
    }

    /// <summary>
    /// Contains the validation rules <see cref="DeletionDaemonDefaultOptions"/>
    /// </summary>
    public class DeletionDeamonOptionsValidationProfile : BackgroundJobSwarmHostOptionsValidationProfile<DeletionDeamonOptions>
    {
        /// <inheritdoc cref="DeletionDeamonOptionsValidationProfile"/>
        public DeletionDeamonOptionsValidationProfile() : base()
        {
            CreateValidationFor<DeletionDeamonOptions>()
                .ForProperty(x => x.AutoManagedDroneCoreMultiplier, x => x.Value)
                    .MustBeLargerOrEqualTo(0d)
                    .MustBeSmallerOrEqualTo(1d);
        }
    }
}
