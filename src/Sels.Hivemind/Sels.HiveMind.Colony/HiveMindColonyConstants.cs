using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.HiveMind.Colony.SystemDaemon;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Contains constant/static read only properties related to HiveMind.
    /// </summary>
    public static class HiveMindColonyConstants
    {
        /// <summary>
        /// Static constant/static read only properties related to the deletion daemons.
        /// </summary>
        public static class DeletionDaemon
        {
            /// <summary>
            /// The prefix that will be used to get the distributed lock resource name that <see cref="BulkDeletingDeletionDaemon"/> will use to limit concurrent nodes deleting.
            /// </summary>
            public const string DeletionDaemonDistributedLockPrefix = "$SystemDeleteNodeLease";
        }
    }
}
