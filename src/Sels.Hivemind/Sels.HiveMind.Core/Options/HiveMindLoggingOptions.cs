using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Exposes settings related to logging by HiveMind services.
    /// </summary>
    public class HiveMindLoggingOptions
    {
        /// <summary>
        /// The threshold above which we log a warning if method execution duration goes above it for client services.
        /// </summary>
        public TimeSpan ClientWarningThreshold { get; set; } = TimeSpan.FromMilliseconds(500);
        /// <summary>
        /// The threshold above which we log an error if method execution duration goes above it for client services.
        /// </summary>
        public TimeSpan ClientErrorThreshold { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// The threshold above which we log a warning if method execution duration goes above it for other services.
        /// </summary>
        public TimeSpan ServiceWarningThreshold { get; set; } = TimeSpan.FromMilliseconds(250);
        /// <summary>
        /// The threshold above which we log an error if method execution duration goes above it for other services.
        /// </summary>
        public TimeSpan ServiceErrorThreshold { get; set; } = TimeSpan.FromMilliseconds(500);
        /// <summary>
        /// The threshold above which we log a warning if method execution duration goes above it for event/requests handlers.
        /// </summary>
        public TimeSpan EventHandlersWarningThreshold { get; set; } = TimeSpan.FromMilliseconds(50);
        /// <summary>
        /// The threshold above which we log an error if method execution duration goes above it for event/requests handlers.
        /// </summary>
        public TimeSpan EventHandlersErrorThreshold { get; set; } = TimeSpan.FromSeconds(100);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="HiveMindLoggingOptions"/>.
    /// </summary>
    public class HiveMindLoggingOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc/>
        public HiveMindLoggingOptionsValidationProfile()
        {
            CreateValidationFor<HiveMindLoggingOptions>()
                .ForProperty(x => x.ClientErrorThreshold)
                    .ValidIf(x => x.Value > x.Source.ClientWarningThreshold, x => $"Must be larger than <{nameof(x.Source.ClientWarningThreshold)}>")
                .ForProperty(x => x.ServiceErrorThreshold)
                    .ValidIf(x => x.Value > x.Source.ServiceWarningThreshold, x => $"Must be larger than <{nameof(x.Source.ServiceWarningThreshold)}>")
                .ForProperty(x => x.EventHandlersErrorThreshold)
                    .ValidIf(x => x.Value > x.Source.EventHandlersWarningThreshold, x => $"Must be larger than <{nameof(x.Source.EventHandlersWarningThreshold)}>");
        }
    }

}
