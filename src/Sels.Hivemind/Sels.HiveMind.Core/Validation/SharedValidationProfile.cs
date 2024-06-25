using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.Core.Extensions.Text;
using Sels.HiveMind.Storage;
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
    /// Contains validation rules for types shared in other types.
    /// </summary>
    public class SharedValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="SharedValidationProfile"/>
        public SharedValidationProfile()
        {
            CreateValidationFor<IInvocationInfo>()
                .ForProperty(x => x.Type, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNull()
                    .ValidIf(x => (x.Value.IsAbstract && x.Value.IsSealed) || (x.Value.IsClass && !x.Value.IsAbstract), x => $"Must be either a static type or a non abstract class")
                .ForProperty(x => x.MethodInfo, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNull()
                    .NextWhen(x => x.Source.Type != null)
                    .ValidIf(x => x.Source.Type.IsAssignableTo(x.Value.DeclaringType), x => $"Method is from type <{x.Value.ReflectedType}> but invocation type is set to <{x.Source.Type}>")
                .ForProperty(x => x.Arguments)
                    .NextWhen(x => x.Source.MethodInfo != null && x.Value != null)
                    .ValidIf(x =>
                    {
                        var parameterCount = x.Source.MethodInfo.GetParameters().Length;
                        x.ValidatorResult = parameterCount;
                        return x.Value.Count == parameterCount;
                    }, x => $"Amount does not match method argument count. Method expects {x.ValidatorResult} arguments but only {x.Value.Count} are defined");

            CreateValidationFor<ILockInfo>()
                .ForProperty(x => x.LockedBy)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.LockedAtUtc)
                    .CannotBeDefault()
                .ForProperty(x => x.LockHeartbeatUtc)
                    .CannotBeDefault();

            CreateValidationFor<IMiddlewareInfo>()
                .ForProperty(x => x.Type, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNull()
                    .ValidIf(x => x.Value.IsClass && !x.Value.IsAbstract, x => $"Must be a non abstract class");

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
                .ForProperty(x => x.States, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeEmpty()
                    .InvalidIf(x =>
                    {
                        var grouped = x.Value.GroupAsDictionary(x => x.Sequence);

                        var duplicates = grouped.Where(x => x.Value.Count > 1).Select(x => x.Key);
                        x.ValidatorResult = duplicates;
                        return duplicates.HasValue();
                    }, x => $"Cannot contain duplicate sequences. Following sequences were used multiple times: {x.ValidatorResult.CastTo<IEnumerable<long>>().JoinString(", ")}");

            CreateValidationFor<JobStateStorageData>()
                .ForProperty(x => x.OriginalTypeName)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Sequence)
                        .MustBeLargerOrEqualTo(0L)
                .ForProperty(x => x.ElectedDateUtc)
                    .CannotBeDefault();

            CreateValidationFor<StorageProperty>()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.OriginalTypeName)
                    .CannotBeNullOrWhitespace();

            CreateValidationFor<MiddlewareStorageData>()
                .ForProperty(x => x.TypeName)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.ContextTypeName)
                    .NextWhen(x => x.Source.Context != null)
                    .CannotBeNullOrWhitespace();

            CreateValidationFor<InvocationStorageData>()
                .ForProperty(x => x.TypeName)
                    .CannotBeNull()
                .ForProperty(x => x.MethodName)
                    .CannotBeNull()
                .ForElements(x => x.GenericArguments)
                    .CannotBeNull();

            CreateValidationFor<InvocationArgumentStorageData>()
                .ForProperty(x => x.TypeName)
                    .CannotBeNull();

            CreateValidationFor<ActionInfo>()
                .ForProperty(x => x.Type)
                    .CannotBeNull()
                .ForProperty(x => x.ComponentId)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.ExecutionId)
                    .CannotBeDefault()
                .ForProperty(x => x.CreatedAtUtc)
                    .CannotBeDefault();
        }
    }
}
