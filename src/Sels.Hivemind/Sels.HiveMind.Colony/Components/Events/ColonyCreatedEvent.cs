using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Events
{
    /// <summary>
    /// Raised when a new <see cref="IColony"/> is created by a factory.
    /// </summary>
    public class ColonyCreatedEvent
    {
        /// <summary>
        /// The colony that was created.
        /// </summary>
        public IColony Colony { get; }

        /// <inheritdoc cref="ColonyCreatedEvent"/>
        /// <param name="colony"><inheritdoc cref="IColony"/></param>
        public ColonyCreatedEvent(IColony colony)
        {
            Colony = colony.ValidateArgument(nameof(colony));
        }
    }
}
