using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Events
{
    /// <summary>
    /// Raised when the state of a running colony is being persisted to storage.
    /// </summary>
    public class SyncingColonyStateEvent
    {
        // Properties
        /// <summary>
        /// The colony whoes state is being persisted.
        /// </summary>
        public IColony Colony { get; }

        /// <inheritdoc cref="SyncingColonyStateEvent"/>
        /// <param name="colony"><inheritdoc cref="Colony"/></param>
        public SyncingColonyStateEvent(IColony colony)
        {
            Colony = Guard.IsNotNull(colony);
        }
    }
}
