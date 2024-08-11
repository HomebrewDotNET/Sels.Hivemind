using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Colony.Validation;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Validation;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Options
{
    /// <inheritdoc cref="IDeletionDaemonOptions"/>
    public class DeletionDeamonOptions : IDeletionDaemonOptions
    {
        /// <inheritdoc/>
        public double AutoManagedDroneCoreMultiplier { get; set; } = 0.2;
        /// <inheritdoc/>
        public int? Drones { get; set; }
        /// <inheritdoc/>
        public int DequeueSize { get; set; } = 250;
        /// <inheritdoc/>
        public double EarlyFetchThreshold { get; set; } = 0.2;
        /// <inheritdoc/>
        public int MininumBatchSize { get; set; } = 100;
        /// <inheritdoc/>
        public int BatchSize { get; set; } = 100;
    }

    /// <summary>
    /// Contains the validation rules <see cref="DeletionDeamonOptions"/>
    /// </summary>
    public class DeletionDeamonOptionsValidationProfile : BackgroundJobQueueProcessingOptionsValidationProfile
    {
        /// <inheritdoc cref="DeletionDeamonOptionsValidationProfile"/>
        public DeletionDeamonOptionsValidationProfile() : base()
        {

        }
    }
}
