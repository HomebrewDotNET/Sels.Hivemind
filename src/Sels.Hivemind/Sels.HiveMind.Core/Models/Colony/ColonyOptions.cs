﻿using Microsoft.Extensions.Logging;
using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Exposes extra options for colony.
    /// </summary>
    public class ColonyOptions : IColonyOptions
    {
        /// <inheritdoc/>
        public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromMinutes(1);
        /// <inheritdoc/>
        public TimeSpan DaemonManagementInterval { get; set; } = TimeSpan.FromMinutes(1);
        /// <inheritdoc/>
        public LogLevel DefaultDaemonLogLevel { get; set; } = LogLevel.Warning;
        /// <inheritdoc/>
        public ColonyCreationOptions CreationOptions { get; set; } = ColonyCreationOptions.Default;
        /// <inheritdoc/>
        public int MaxScheduleTries { get; set; } = 10;
        /// <inheritdoc/>
        public TimeSpan ReleaseLockTime { get; set; } = TimeSpan.FromSeconds(25);
        /// <inheritdoc/>
        public TimeSpan DaemonMaxStopTime { get; set; } = TimeSpan.FromSeconds(25);
        /// <inheritdoc/>
        public TimeSpan StateSyncInterval { get; set; } = TimeSpan.FromSeconds(2);
        /// <inheritdoc/>
        public DeletionMode DeletionMode { get; set; } = DeletionMode.Bulk;
        /// <inheritdoc/>
        public TimeSpan InactiveColonyRetention { get; set; } = TimeSpan.FromDays(30);
        /// <inheritdoc/>
        public TimeSpan InactiveColonyManagementInterval { get; set; } = TimeSpan.FromDays(1);
        /// <inheritdoc/>
        public ColonyDaemonRetentionMode DaemonLogRetentionMode { get; set; } = ColonyDaemonRetentionMode.Amount;
        /// <inheritdoc/>
        public int DaemonLogRetentionAmount { get; set; } = 1000;
        /// <inheritdoc/>
        public TimeSpan DaemonLogRetentionManagementInterval { get; set; } = TimeSpan.FromHours(6);

        /// <inheritdoc cref="ColonyOptions"/>
        public ColonyOptions()
        {
            
        }

        /// <inheritdoc cref="ColonyOptions"/>
        /// <param name="options">The options to create from</param>
        public ColonyOptions(IColonyOptions options)
        {
            options = Guard.IsNotNull(options);

            ErrorRetryDelay = options.ErrorRetryDelay;
            DaemonManagementInterval = options.DaemonManagementInterval;
            DefaultDaemonLogLevel = options.DefaultDaemonLogLevel;
            CreationOptions = options.CreationOptions;
            MaxScheduleTries = options.MaxScheduleTries;
            ReleaseLockTime = options.ReleaseLockTime;
            DaemonMaxStopTime = options.DaemonMaxStopTime;
            StateSyncInterval = options.StateSyncInterval;
            DeletionMode = options.DeletionMode;
            InactiveColonyRetention = options.InactiveColonyRetention;
            InactiveColonyManagementInterval = options.InactiveColonyManagementInterval;
            DaemonLogRetentionMode = options.DaemonLogRetentionMode;
            DaemonLogRetentionAmount = options.DaemonLogRetentionAmount;
            DaemonLogRetentionManagementInterval = options.DaemonLogRetentionManagementInterval;
        }
    }

    /// <summary>
    /// Contains the validation rules for <see cref="ColonyOptions"/>.
    /// </summary>
    public class HiveColonyOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="HiveColonyOptionsValidationProfile"/>
        public HiveColonyOptionsValidationProfile()
        {
            CreateValidationFor<IColonyOptions>()
                .ForProperty(x => x.MaxScheduleTries)
                    .MustBeLargerOrEqualTo(0)
                .ForProperty(x => x.DaemonLogRetentionAmount)
                    .MustBeLargerOrEqualTo(0);
        }
    }
}
