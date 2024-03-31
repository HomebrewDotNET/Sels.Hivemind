using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage.Job;
using Sels.ObjectValidationFramework.Profile;
using Sels.ObjectValidationFramework.Target;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind.Validation
{
    /// <summary>
    /// Contains validation rules for objects related to recurring jobs.
    /// </summary>
    public class RecurringJobValidationProfile : SharedValidationProfile
    {
        /// <inheritdoc/>
        public RecurringJobValidationProfile()
        {
            CreateValidationFor<IReadOnlyRecurringJob>()
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
                .ForProperty(x => x.Schedule)
                    .CannotBeNull()
                .ForProperty(x => x.Settings)
                    .CannotBeNull()
                .ForElements(x => x.Properties, x => x.Key)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.CreatedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.ModifiedAtUtc)
                    .CannotBeDefault();

            CreateValidationFor<RecurringJobConfigurationStorageData>()
                .ForProperty(x => x.Queue, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.QueueNameRegex)
                .ForProperty(x => x.Requester)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.InvocationData)
                    .CannotBeNull()
                .ForProperty(x => x.Schedule)
                    .CannotBeNull()
                .ForProperty(x => x.Settings)
                    .CannotBeNull()
                .ForProperty(x => x.ModifedAt)
                    .CannotBeDefault();

            CreateValidationFor<RecurringJobStorageData>()
                .ForProperty(x => x.Queue, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.QueueNameRegex)
                .ForProperty(x => x.ExecutionId)
                    .CannotBeDefault()
                .ForProperty(x => x.InvocationData)
                    .CannotBeNull()
                .ForProperty(x => x.Schedule)
                    .CannotBeNull()
                .ForProperty(x => x.Settings)
                    .CannotBeNull()
                .ForProperty(x => x.CreatedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.ModifiedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.States, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeEmpty()
                    .InvalidIf(x =>
                    {
                        var grouped = x.Value.GroupAsDictionary(x => x.Sequence);

                        var duplicates = grouped.Where(x => x.Value.Count > 1).Select(x => x.Key);
                        x.ValidatorResult = duplicates;
                        return duplicates.HasValue();
                    }, x => $"Cannot contain duplicate sequences. Following sequences were used multiple times: {x.ValidatorResult.CastTo<IEnumerable<long>>().JoinString(", ")}");

            CreateValidationFor<RecurringJobStateStorageData>()
                .ForProperty(x => x.Sequence)
                    .MustBeLargerOrEqualTo(0L);

            CreateValidationFor<IMiddlewareInfo>()
                .ForProperty(x => x.Type)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Value.IsAssignableTo<IRecurringJobMiddleware>(), x => $"Must be assignable to type <{typeof(IRecurringJobMiddleware)}>");

            CreateValidationFor<ActionInfo>()
                .ForProperty(x => x.Type)
                    .NextWhenNotNull()
                    .ValidIf(x => x.Value.IsAssignableTo<IRecurringJobAction>(), x => $"Must be assignable to type <{typeof(IRecurringJobAction)}>");

            ImportFrom<ScheduleValidationProfile>();
        }
    }
}
