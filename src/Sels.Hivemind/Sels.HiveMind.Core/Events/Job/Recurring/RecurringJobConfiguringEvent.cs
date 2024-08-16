using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Events.Job.Recurring
{
    /// <summary>
    /// Raised when a recurring job is being configured.
    /// Will be created or updated afterwards.
    /// </summary>
    public class RecurringJobConfiguringEvent
    {
        /// <summary>
        /// The builder that is used to configure the recurring job.
        /// </summary>
        public IRecurringJobBuilder Builder { get; }

        /// <inheritdoc cref="RecurringJobConfiguringEvent"/>
        /// <param name="builder"><inheritdoc cref="Builder"/></param>
        public RecurringJobConfiguringEvent(IRecurringJobBuilder builder)
        {
            Builder = builder.ValidateArgument(nameof(builder));
        }
    }
}
