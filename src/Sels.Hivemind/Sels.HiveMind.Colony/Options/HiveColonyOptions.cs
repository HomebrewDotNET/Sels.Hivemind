using Sels.ObjectValidationFramework.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Exposes extra options for <see cref="HiveColony"/>.
    /// </summary>
    public class HiveColonyOptions
    {
        /// <summary>
        /// How often the colony should check it's daemons.
        /// </summary>
        public TimeSpan DaemonManagementInterval { get; set; } = TimeSpan.FromMinutes(1);
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
