using Microsoft.Extensions.Logging;
using Sels.ObjectValidationFramework.Profile;
using Sels.HiveMind.Colony.SystemDaemon;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Exposes extra options for <see cref="HiveColony"/>.
    /// </summary>
    public class HiveColonyOptions : IColonyOptions
    {
        /// <inheritdoc/>
        public TimeSpan DaemonManagementInterval { get; set; } = TimeSpan.FromMinutes(1);
        /// <inheritdoc/>
        public LogLevel DefaultDaemonLogLevel { get; set; } = LogLevel.Warning;
        /// <inheritdoc/>
        public HiveColonyCreationOptions CreationOptions { get; set; } = HiveColonyCreationOptions.Default;
    }

    /// <summary>
    /// Determines the creation options for a colony.
    /// </summary>
    [Flags]
    public enum HiveColonyCreationOptions
    {
        /// <summary>
        /// No options selected.
        /// </summary>
        None = 0,
        /// <summary>
        /// The default options.
        /// </summary>
        Default = AutoCreateLockMonitor,
        /// <summary>
        /// Enables the creation of <see cref="LockMonitorDaemon"/>.
        /// </summary>
        AutoCreateLockMonitor = 1
    }

    /// <summary>
    /// Contains the validation rules for <see cref="HiveColonyOptions"/>.
    /// </summary>
    public class HiveColonyOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="HiveColonyOptionsValidationProfile"/>
        public HiveColonyOptionsValidationProfile()
        {
            
        }
    }
}
