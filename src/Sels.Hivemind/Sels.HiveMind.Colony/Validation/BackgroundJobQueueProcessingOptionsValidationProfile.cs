using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Validation
{
    /// <summary>
    /// Contains the validation rules for <see cref="IBackgroundJobQueueProcessorOptions"/>.
    /// </summary>
    public class BackgroundJobQueueProcessingOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="BackgroundJobQueueProcessingOptionsValidationProfile"/>
        public BackgroundJobQueueProcessingOptionsValidationProfile()
        {
            CreateValidationFor<IBackgroundJobQueueProcessorOptions>()
                .ForProperty(x => x.AutoManagedDroneCoreMultiplier)
                    .MustBeLargerOrEqualTo(0d)
                    .MustBeSmallerOrEqualTo(1d)
                .ForProperty(x => x.Drones, x => x.Value)
                    .MustBeLargerOrEqualTo(1)
                .ForProperty(x => x.DequeueSize)
                    .MustBeLargerOrEqualTo(1)
                    .ValidIf(x => x.Value <= HiveMindConstants.Query.MaxDequeueLimit, x => $"Must be smaller or equal to <{HiveMindConstants.Query.MaxDequeueLimit}>")
                .ForProperty(x => x.MininumBatchSize)
                    .MustBeLargerOrEqualTo(1)
                    .ValidIf(x => x.Value <= x.Source.DequeueSize, x => $"Must be smaller or equal to <{nameof(x.Source.DequeueSize)}>")
                .ForProperty(x => x.BatchSize)
                    .MustBeLargerOrEqualTo(1)
                    .ValidIf(x => x.Value <= x.Source.DequeueSize, x => $"Must be smaller or equal to <{nameof(x.Source.DequeueSize)}>");
        }
    }
}
