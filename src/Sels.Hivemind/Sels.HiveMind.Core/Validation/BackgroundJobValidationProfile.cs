using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.ObjectValidationFramework.Profile;
using Sels.ObjectValidationFramework.Target;
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
                .ForProperty(x => x.Queue, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.QueueNameRegex)
                .ForProperty(x => x.Environment, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.EnvironmentRegex)
                .ForProperty(x => x.ExecutionId)
                    .CannotBeDefault()
                .ForProperty(x => x.State)
                    .CannotBeNull()
                .ForProperty(x => x.Invocation)
                    .CannotBeNull()
                .ForElements(x => x.Properties, x => x.Key)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.CreatedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.ModifiedAtUtc)
                    .CannotBeDefault();

            CreateValidationFor<JobStorageData>()
                .ForProperty(x => x.Queue, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.QueueNameRegex)
                .ForProperty(x => x.ExecutionId)
                    .CannotBeDefault()
                .ForProperty(x => x.InvocationData)
                    .CannotBeNull()
                .ForProperty(x => x.CreatedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.ModifiedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.States)
                    .CannotBeEmpty();

            CreateValidationFor<JobStateStorageData>()
                .ForProperty(x => x.OriginalTypeName)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.ElectedDateUtc)
                    .CannotBeDefault();
        }
    }
}
