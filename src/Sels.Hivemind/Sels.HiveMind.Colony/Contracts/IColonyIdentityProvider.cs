using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Generates globally unique names for <see cref="IColony"/>(s).
    /// </summary>
    public interface IColonyIdentityProvider
    {
        /// <summary>
        /// Generates a unique name for a <see cref="IColony"/>.
        /// </summary>
        /// <param name="colony">The colony to generate the name for</param>
        /// <returns>A unique name for a <see cref="IColony"/></returns>
        public string GenerateName(IReadOnlyColony colony);
    }
}
