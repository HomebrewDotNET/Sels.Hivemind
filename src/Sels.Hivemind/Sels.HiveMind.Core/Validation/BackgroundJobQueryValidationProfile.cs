using Sels.Core.Extensions.Equality;
using Sels.HiveMind.Job;
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
    /// Contains validation rules for objects related to background jobs.
    /// </summary>
    public class BackgroundJobQueryValidationProfile : SharedQueryValidationProfile
    {
        /// <inheritdoc cref="BackgroundJobValidationProfile"/>
        public BackgroundJobQueryValidationProfile()
        {
            CreateValidationFor<BackgroundJobConditionGroupExpression>()
                .ForProperty(x => x.Expression)
                    .CannotBeNull();

            CreateValidationFor<BackgroundJobConditionExpression>()
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

            CreateValidationFor<BackgroundJobCondition>()
                    .Switch(x => x.Target)
                        .Case(QueryBackgroundJobConditionTarget.Queue)
                            .Then(x => x.ForProperty(x => x.QueueComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.LockedBy)
                            .Then(x => x.ForProperty(x => x.LockedByComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.Priority)
                            .Then(x => x.ForProperty(x => x.PriorityComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.Property)
                            .Then(x => x.ForProperty(x => x.PropertyComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.CurrentState)
                            .Then(x => x.ForProperty(x => x.CurrentStateComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.PastState)
                            .Then(x => x.ForProperty(x => x.PastStateComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.CreatedAt)
                            .Then(x => x.ForProperty(x => x.CreatedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                        .Case(QueryBackgroundJobConditionTarget.ModifiedAt)
                            .Then(x => x.ForProperty(x => x.ModifiedAtComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"));

            CreateValidationFor<BackgroundJobStateCondition>()
                .Switch(x => x.Target)
                    .Case(QueryBackgroundJobStateConditionTarget.Name)
                        .Then(x => x.ForProperty(x => x.NameComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryBackgroundJobStateConditionTarget.ElectedDate)
                        .Then(x => x.ForProperty(x => x.ElectedDateComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryBackgroundJobStateConditionTarget.Reason)
                        .Then(x => x.ForProperty(x => x.ReasonComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"))
                    .Case(QueryBackgroundJobStateConditionTarget.Property)
                        .Then(x => x.ForProperty(x => x.PropertyComparison).CannotBeNull(x => $"Cannot be null when {nameof(x.Source.Target)} is set to <{x.Source.Target}>"));

            CreateValidationFor<BackgroundJobPropertyCondition>()
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
        }
    }
}
