using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Options for configuring the setting for retrying failed background jobs.
    /// </summary>
    public class BackgroundJobRetryOptions
    {
        // Fields
        private readonly Dictionary<Type, Predicate<Exception>> _fatalExceptions = new Dictionary<Type, Predicate<Exception>>();

        // Properties
        /// <summary>
        /// The maximum amount of times a background job can be retried.
        /// </summary>
        public int MaxRetryCount { get; } = 10;
        /// <summary>
        /// Contains how long to wait before retrying a job. 
        /// Element is taken based on the current retry count of the job.
        /// </summary>
        public TimeSpan[] RetryTimes { get; } = new TimeSpan[]
        {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(90),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromSeconds(150),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(210),
            TimeSpan.FromMinutes(4),
            TimeSpan.FromSeconds(270),
            TimeSpan.FromMinutes(5),
        };

        /// <inheritdoc/>
        public BackgroundJobRetryOptions()
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
        /// Adds exception of type <typeparamref name="T"/> so it won't be retried.
        /// </summary>
        /// <typeparam name="T">The type of the exception to add</typeparam>
        /// <param name="condition">Optional condition for the exception. Return true to not retry, false to retry</param>
        public void AddFatalException<T>(Predicate<T> condition = null) where T : Exception
        {
            lock (_fatalExceptions)
            {
                _fatalExceptions.AddOrUpdate(typeof(T), condition != null ? e => condition(e.CastTo<T>()) : (Predicate<Exception>)null);
            }
        }
    }

    /// <summary>
    /// Contains the validation rules for <see cref="BackgroundJobRetryOptions"/>.
    /// </summary>
    public class BackgroundJobRetryOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="BackgroundJobRetryOptionsValidationProfile"/>
        public BackgroundJobRetryOptionsValidationProfile()
        {
            CreateValidationFor<BackgroundJobRetryOptions>()
                .ForProperty(x => x.MaxRetryCount)
                    .MustBeLargerOrEqualTo(0)
                .ForProperty(x => x.RetryTimes)
                    .CannotBeEmpty();
        }
    }
}
