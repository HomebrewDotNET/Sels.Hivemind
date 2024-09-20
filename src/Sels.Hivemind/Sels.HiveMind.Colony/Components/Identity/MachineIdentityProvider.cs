using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Sels.HiveMind.Colony.Identity
{
    /// <summary>
    /// <inheritdoc cref="IColonyIdentityProvider"/>.
    /// Generates an identity by using the machine name
    /// </summary>
    public class MachineIdentityProvider : IColonyIdentityProvider
    {
        /// <inheritdoc/>
        public string GenerateId(IReadOnlyColony colony) => Environment.MachineName;
    }
}
