﻿using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.HiveMind.Job;
using Sels.HiveMind.Job;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Job.Background;
using Sels.HiveMind.Job.Recurring;
using Sels.HiveMind.Job.State;
using Sels.HiveMind.Job.State.Background;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains extra configuration for hive mind components.
    /// </summary>
    public class HiveMindOptions
    {
        // Fields
        private readonly Dictionary<Type, Predicate<Exception>?> _fatalExceptions = new Dictionary<Type, Predicate<Exception>?>();

        /// <summary>
        /// The prefix that will used for all cache keys.
        /// </summary>
        public string CachePrefix { get; set; } = "Sels.HiveMind";
        /// <summary>
        /// How long delegates generated by HiveMind stay cached.
        /// </summary>
        public TimeSpan DelegateExpiryTime { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// How long cached values by type converters will stay in the cache.
        /// </summary>
        public TimeSpan TypeConversionCacheRetention { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long after the last heartbeat on a lock before it's considered timed out.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// If the remaining time before a lock can time out is below the offset, the heartbeat will be set to ensure actions are executed with a valid lock. 
        /// Also used by workers to maintain the heartbeat on an active lock.
        /// (e.g. 5 seconds before a lock can time out a save is requested on a job, with the offset set to 10 seconds the heartbeat will be set before performing the save)
        /// </summary>
        public TimeSpan LockExpirySafetyOffset { get; set; } = TimeSpan.FromSeconds(31);

        /// <summary>
        /// How long completed background jobs are kept before deletion is triggered.
        /// When set to null no cleanup will be performed.
        /// </summary>
        public TimeSpan? CompletedBackgroundJobRetention { get; set; } = TimeSpan.FromDays(30);
        /// <summary>
        /// The names of the states that are considered as completed states for background jobs.
        /// </summary>
        public string[] CompletedBackgroundJobStateNames { get; set; } = new string[] { SucceededState.StateName, DeletedState.StateName };

        /// <summary>
        /// The default recurring job settings that will be used by recurring jobs
        /// </summary>
        public RecurringJobSettings RecurringJobSettings { get; set; } = new RecurringJobSettings()
        {
            ScheduleTime = ScheduleTime.CompletedDate,
            MaxScheduleTries = 10,
            CanMisfire = false,
            MisfireBehaviour = MisfireBehaviour.Schedule,
            MisfireThreshold = TimeSpan.FromMinutes(1),
            StateRetentionMode = RecurringJobRetentionMode.OlderThan,
            StateRetentionAmount = 14,
            LogRetentionMode = RecurringJobRetentionMode.Amount,
            LogRetentionAmount = 1000
        };
        /// <summary>
        /// The maximum amount of time a client will wait to lock a recurring job before timing out.
        /// When set to null the client will wait indefinitely.
        /// </summary>
        public TimeSpan? DefaultRecurringJobUpdateTimeout { get; set; }

        /// <inheritdoc/>
        public HiveMindOptions()
        {
            AddFatalException<SystemException>();
        }

        internal bool IsFatal(Exception exception)
        {
            exception.ValidateArgument(nameof(exception));

            var matchedType = _fatalExceptions.Keys.FirstOrDefault(x => exception.IsAssignableTo(x));

            if (matchedType != null)
            {
                var predicate = _fatalExceptions[matchedType];

                return predicate != null ? predicate(exception) : true;
            }

            return false;
        }
        /// <summary>
        /// Adds exception of type <typeparamref name="T"/> so it won't be retried by jobs.
        /// </summary>
        /// <typeparam name="T">The type of the exception to add</typeparam>
        /// <param name="condition">Optional condition for the exception. Return true to not retry, false to retry</param>
        public void AddFatalException<T>(Predicate<T>? condition = null) where T : Exception
        {
            lock (_fatalExceptions)
            {
                _fatalExceptions.AddOrUpdate(typeof(T), condition != null ? e => condition(e.CastTo<T>()) : (Predicate<Exception>?)null);
            }
        }
    }

    /// <summary>
    /// Contains validation rules for <see cref="HiveMindOptions"/>.
    /// </summary>
    public class HiveMindOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="HiveMindOptionsValidationProfile"/>
        public HiveMindOptionsValidationProfile()
        {
            CreateValidationFor<HiveMindOptions>()
                .ForProperty(x => x.CachePrefix)
                    .CannotBeNullOrWhitespace()
                .ForProperty(x => x.DelegateExpiryTime)
                    .MustBeLargerOrEqualTo(TimeSpan.Zero)
                .ForProperty(x => x.TypeConversionCacheRetention)
                    .MustBeLargerOrEqualTo(TimeSpan.Zero)
                .ForProperty(x => x.LockTimeout)
                    .MustBeLargerOrEqualTo(TimeSpan.FromMinutes(1))
                .ForProperty(x => x.LockExpirySafetyOffset)
                    .ValidIf(x => x.Value < x.Source.LockTimeout, x => $"Must be smaller than {nameof(x.Source.LockTimeout)}")
                    .MustBeLargerOrEqualTo(TimeSpan.FromSeconds(1))
                .ForProperty(x => x.CompletedBackgroundJobStateNames)
                    .CannotBeEmpty();
        }
    }
}
