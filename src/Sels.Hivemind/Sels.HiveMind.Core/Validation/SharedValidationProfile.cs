﻿using Sels.Core.Extensions.Reflection;
using Sels.ObjectValidationFramework.Profile;
using Sels.ObjectValidationFramework.Target;
using System;
using System.Collections.Generic;
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
                    .CannotBeNullOrWhitespace();

            CreateValidationFor<IMiddlewareInfo>()
                .ForProperty(x => x.Type, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNull()
                    .ValidIf(x => x.Value.IsClass && !x.Value.IsAbstract, x => $"Must be a non abstract class");
        }
    }
}