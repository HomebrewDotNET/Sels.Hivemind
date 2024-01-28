using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony.Identity
{
    /// <summary>
    /// <inheritdoc cref="IColonyIdentityProvider"/>.
    /// Generates a guid as name.
    /// </summary>
    public class GuidIdentityProvider : IColonyIdentityProvider
    {
        /// <inheritdoc/>
        public string GenerateName(IReadOnlyColony colony) => Guid.NewGuid().ToString();
    }
}
