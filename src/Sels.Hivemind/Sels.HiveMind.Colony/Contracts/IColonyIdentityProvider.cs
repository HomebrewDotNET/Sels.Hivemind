using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Generates identities for <see cref="IColony"/>(s).
    /// </summary>
    public interface IColonyIdentityProvider
    {
        /// <summary>
        /// Generates an identity for a <see cref="IColony"/>.
        /// </summary>
        /// <param name="colony">The colony to generate the name for</param>
        /// <returns>An identity for a <see cref="IColony"/></returns>
        public string GenerateName(IReadOnlyColony colony);
    }
}
