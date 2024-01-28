using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Events
{
    /// <summary>
    /// Raised when a <see cref="IReadOnlyColony"/> changes status.
    /// </summary>
    public class ColonyStatusChangedEvent
    {
        /// <summary>
        /// The colony that changed status
        /// </summary>
        public IReadOnlyColony Colony { get; }

        /// <inheritdoc cref="ColonyStatusChangedEvent"/>
        public ColonyStatusChangedEvent(IReadOnlyColony colony)
        {
            Colony = colony.ValidateArgument(nameof(colony));
        }
    }
}
