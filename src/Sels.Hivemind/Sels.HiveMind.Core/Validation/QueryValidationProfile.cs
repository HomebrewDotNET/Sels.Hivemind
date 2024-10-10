using Sels.Core.Extensions.Equality;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query;
using Sels.HiveMind.Query.Colony;
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
    /// Contains validation rules for objects related querying entities.
    /// </summary>
    public class QueryValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="QueryValidationProfile"/>
        public QueryValidationProfile()
        {
            CreateValidationFor<JobConditionGroupExpression>()
                .ForProperty(x => x.Expression)
                    .CannotBeNull();
            CreateValidationFor<ColonyConditionGroupExpression>()
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
            CreateValidationFor<ColonyConditionExpression>()
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
                        .Case(QueryJobConditionTarget.Id)
                            .Then(x => x.ForProperty(x => x.IdComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
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

            CreateValidationFor<ColonyCondition>()
                .Switch(x => x.Target)
                    .Case(QueryColonyConditionTarget.Id)
                         .Then(x => x.ForProperty(x => x.IdComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyConditionTarget.Name)
                         .Then(x => x.ForProperty(x => x.NameComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyConditionTarget.Status)
                         .Then(x => x.ForProperty(x => x.StatusComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyConditionTarget.Daemon)
                         .Then(x => x.ForProperty(x => x.DaemonCondition).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyConditionTarget.Property)
                         .Then(x => x.ForProperty(x => x.PropertyComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyConditionTarget.CreatedAt)
                         .Then(x => x.ForProperty(x => x.CreatedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyConditionTarget.ModifiedAt)
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

            CreateValidationFor<ColonyDaemonCondition>()
                .Switch(x => x.Target)
                    .Case(QueryColonyDaemonConditionTarget.Name)
                        .Then(x => x.ForProperty(x => x.NameComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyDaemonConditionTarget.Status)
                        .Then(x => x.ForProperty(x => x.StatusComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyDaemonConditionTarget.CreatedAt)
                        .Then(x => x.ForProperty(x => x.CreatedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyDaemonConditionTarget.ModifiedAt)
                        .Then(x => x.ForProperty(x => x.ModifiedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryColonyDaemonConditionTarget.Property)
                        .Then(x => x.ForProperty(x => x.PropertyComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"));

            CreateValidationFor<PropertyCondition>()
                .ForProperty(x => x.Name)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.Type)
                    .ValidIf(x => x.Value != Storage.StorageType.Serialized, x => $"Cannot be set to {x.Value} because this type isn't queryable")
                .ForProperty(x => x.Comparison)
                    .NextWhen(x => x.Source.QueryType == PropertyConditionQueryType.Value)
                    .CannotBeNull()
                .ForProperty(x => x.Comparison)
                    .NextWhen(x => x.Source.QueryType != PropertyConditionQueryType.Value)
                    .MustBeNull()
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
                                .MustBeNull(x => $"Must be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>")
                             .ForProperty(x => x.Value)
                                .CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Comparator)} is set to <{x.Source.Comparator}>");
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
