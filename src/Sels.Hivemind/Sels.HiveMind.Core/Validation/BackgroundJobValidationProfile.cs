using Sels.HiveMind.Job;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Validation
{
    /// <summary>
    /// Contains validation rules for objects related to background jobs.
    /// </summary>
    public class BackgroundJobValidationProfile : SharedValidationProfile
    {
        /// <inheritdoc cref="BackgroundJobValidationProfile"/>
        public BackgroundJobValidationProfile()
        {
            CreateValidationFor<IReadOnlyBackgroundJob>()
                .ForProperty(x => x.Queue)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.QueueNameRegex)
                .ForProperty(x => x.Environment)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.EnvironmentRegex)
                .ForProperty(x => x.ExecutionId)
                    .CannotBeDefault()
                .ForProperty(x => x.State)
                    .CannotBeNull()
                .ForElements(x => x.Properties, x => x.Key)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.CreatedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.ModifiedAtUtc)
                    .CannotBeDefault();
        }
    }
}
