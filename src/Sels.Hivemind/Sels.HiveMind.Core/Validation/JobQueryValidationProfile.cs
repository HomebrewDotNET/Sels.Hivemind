using Sels.Core.Extensions.Equality;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Storage.Job;
using Sels.ObjectValidationFramework.Profile;
using Sels.ObjectValidationFramework.Target;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Validation
{
    /// <summary>
    /// Contains validation rules for objects related querying jobs.
    /// </summary>
    public class JobQueryValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="JobQueryValidationProfile"/>
        public JobQueryValidationProfile()
        {
            CreateValidationFor<JobConditionGroupExpression>()
                .ForProperty(x => x.Expression)
                    .CannotBeNull();

            CreateValidationFor<JobConditionExpression>()
                .Switch(x => x.IsGroup)
                    .Case(true)
                        .Then(x =>
                        {
                            x.ForProperty(x => x.Group)
                                .CannotBeNull(x => $"Cannot be null when {nameof(x.Source.IsGroup)} is set to true")
                            .ForProperty(x => x.Condition)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.IsGroup)} is set to true");
                        })
                    .Case(false)
                        .Then(x =>
                        {
                            x.ForProperty(x => x.Group)
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.IsGroup)} is set to false")
                            .ForProperty(x => x.Condition)
                                .CannotBeNull(x => $"Cannot be null when {nameof(x.Source.IsGroup)} is set to false");
                        });

            CreateValidationFor<JobCondition>()
                    .Switch(x => x.Target)
                        .Case(QueryJobConditionTarget.Queue)
                            .Then(x => x.ForProperty(x => x.QueueComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.LockedBy)
                            .Then(x => x.ForProperty(x => x.LockedByComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.Priority)
                            .Then(x => x.ForProperty(x => x.PriorityComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.Property)
                            .Then(x => x.ForProperty(x => x.PropertyComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.CurrentState)
                            .Then(x => x.ForProperty(x => x.CurrentStateComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.PastState)
                            .Then(x => x.ForProperty(x => x.PastStateComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.CreatedAt)
                            .Then(x => x.ForProperty(x => x.CreatedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryJobConditionTarget.ModifiedAt)
                            .Then(x => x.ForProperty(x => x.ModifiedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"));

            CreateValidationFor<JobStateCondition>()
                .Switch(x => x.Target)
                    .Case(QueryJobStateConditionTarget.Name)
                        .Then(x => x.ForProperty(x => x.NameComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryJobStateConditionTarget.ElectedDate)
                        .Then(x => x.ForProperty(x => x.ElectedDateComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryJobStateConditionTarget.Reason)
                        .Then(x => x.ForProperty(x => x.ReasonComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryJobStateConditionTarget.Property)
                        .Then(x => x.ForProperty(x => x.PropertyComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"));

            CreateValidationFor<JobPropertyCondition>()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Type)
                    .ValidIf(x => x.Value != Storage.StorageType.Serialized, x => $"Cannot be set to {x.Value} because this type isn't queryable")
                .ForProperty(x => x.Comparison)
                    .CannotBeNull()
                .ValidateNextWhen(x => x.Source.Comparison != null)
                .Switch(x => x.Type)
                    .Case(x => x.In(Storage.StorageType.Number, Storage.StorageType.FloatingNumber, Storage.StorageType.Date))
                        .Then(x =>
                        {
                            x.ForSource(x => x.Comparison.Comparator)
                                .InvalidIf(x => x.Value == Query.QueryComparator.Like, x => $"{nameof(x.Source.Comparison.Comparator)} cannot be set to <{x.Value}> when {nameof(x.Source.Type)} is set to <{x.Source.Type}>");
                        });

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
