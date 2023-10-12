﻿using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql
{
    /// <summary>
    /// Contains extra options for MySQL HiveMind storage.
    /// </summary>
    public class HiveMindMySqlStorageOptions
    {
        /// <summary>
        /// Set to true to automatically deploy the database schema for each configured environment.
        /// </summary>
        public bool DeploySchema { get; set; } = true;
        /// <summary>
        /// The name of the distributed lock that will be used to synchronize deployments.
        /// </summary>
        public string DeploymentLockName { get; set; } = "Deployment";
        /// <summary>
        /// How long to wait for the deployment lock before throwing an error.
        /// </summary>
        public TimeSpan DeploymentLockTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }
    /// <summary>
    /// Contains the validation rules for <see cref="HiveMindMySqlStorageOptions"/>.
    /// </summary>
    public class HiveMindMySqlStorageOptionsValidationProfile : ValidationProfile<string>
    {
        /// <inheritdoc cref="HiveMindMySqlStorageOptionsValidationProfile"/>
        public HiveMindMySqlStorageOptionsValidationProfile()
        {
            CreateValidationFor<HiveMindMySqlStorageOptions>()
                .ForProperty(x => x.DeploymentLockName)
                    .CannotBeNullOrWhitespace();
        }
    }
}
