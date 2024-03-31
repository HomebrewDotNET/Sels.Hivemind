using Sels.Core.Extensions.Reflection;
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

            CreateValidationFor<BackgroundJobStorageData>()
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

            CreateValidationFor<IMiddlewareInfo>()
                .ForProperty(x => x.Type)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Value.IsAssignableTo<IBackgroundJobMiddleware>(), x => $"Must be assignable to type <{typeof(IBackgroundJobMiddleware)}>");

            CreateValidationFor<ActionInfo>()
                .ForProperty(x => x.Type)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Value.IsAssignableTo<IBackgroundJobAction>(), x => $"Must be assignable to type <{typeof(IBackgroundJobAction)}>");
        }
    }
}
