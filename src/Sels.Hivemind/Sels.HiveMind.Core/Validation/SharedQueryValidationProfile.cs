using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Query;
using Sels.HiveMind.Storage;
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
    public class SharedQueryValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="SharedQueryValidationProfile"/>
        public SharedQueryValidationProfile()
        {
            CreateValidationFor<QueryComparison>()
                .Switch(x => x.Comparator)
                    .Case(QueryComparator.Equals)
                        .Then(b =>
                        {
                            b.ForProperty(x => x.Values, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Pattern, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>");
                        })
                    .Case(x => x.In(QueryComparator.GreaterThan, QueryComparator.LesserThan, QueryComparator.GreaterOrEqualTo, QueryComparator.LesserOrEqualTo))
                        .Then(b =>
                        {
                            b.ForProperty(x => x.Values, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Pattern, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Value)
                                .CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>");
                        })
                    .Case(QueryComparator.Like)
                        .Then(b =>
                        {
                            b.ForProperty(x => x.Values, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Pattern, TargetExecutionOptions.ExitOnInvalid)
                                .CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                                .MustContainAtLeast(2, x => $"Must contain at least 2 elements when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Value, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>");
                        })
                    .Case(QueryComparator.In)
                        .Then(b =>
                        {
                            b.ForProperty(x => x.Values, TargetExecutionOptions.ExitOnInvalid)
                                .CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                                .MustContainAtLeast(1, x => $"Must contain at least 2 elements when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Pattern, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Value, TargetExecutionOptions.ExitOnInvalid)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>");
                        });
        }
    }
}
