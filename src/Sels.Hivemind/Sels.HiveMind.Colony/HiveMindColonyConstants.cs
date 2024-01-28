using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains constant/static read only properties related to HiveMind.
    /// </summary>
    public static class HiveMindColonyConstants
    {
        /// <summary>
        /// Contains constant/static read only properties related to daemons.
        /// </summary>
        public static class Daemon
        {
            /// <summary>
            /// The name of the property set on daemons to indicate they were auto created.
            /// </summary>
            public const string IsAutoCreatedProperty = "IsAutoCreated";
        }
    }
}
