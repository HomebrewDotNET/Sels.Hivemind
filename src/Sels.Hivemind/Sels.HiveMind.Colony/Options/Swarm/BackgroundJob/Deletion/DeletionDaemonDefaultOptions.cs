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

namespace Sels.HiveMind.Colony.Swarm.BackgroundJob.Deletion
{
    /// <summary>
    /// Contains the default values that will be used for missing config in <see cref="DeletionDeamonOptions"/>
    /// </summary>
    public class DeletionDaemonDefaultOptions : BackgroundJobSwarmHostDefaultOptions
    {
        /// <summary>
        /// Contains the default value for <see cref="DeletionDeamonOptions.ErrorDelay"/>.
        /// </summary>
        public TimeSpan ErrorDelay { get; set; } = TimeSpan.FromMinutes(5);
        /// <summary>
        /// Contains the default value for <see cref="DeletionDeamonOptions.AllowedDeviation"/>.
        /// </summary>
        public TimeSpan AllowedDeviation { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// Contains the default value for <see cref="DeletionDeamonOptions.AutoManagedDroneCoreMultiplier"/>.
        /// </summary>
        public double AutoManagedDroneCoreMultiplier { get; set; } = 25;
        /// <summary>
        /// Contains the default value for <see cref="DeletionDeamonOptions.AutoManagedRestartInterval"/>.
        /// </summary>
        public TimeSpan AutoManagedRestartInterval { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Contains the validation rules <see cref="DeletionDaemonDefaultOptions"/>
    /// </summary>
    public class DeletionDeamonDefaultOptionsValidationProfile : BackgroundJobSwarmHostDefaultOptionsValidationProfile
    {
        /// <inheritdoc cref="DeletionDeamonOptionsValidationProfile"/>
        public DeletionDeamonDefaultOptionsValidationProfile() : base()
        {
            CreateValidationFor<DeletionDaemonDefaultOptions>()
                .ForProperty(x => x.AutoManagedDroneCoreMultiplier)
                    .MustBeLargerOrEqualTo(0d)
                    .MustBeSmallerOrEqualTo(1d);
        }
    }
}
