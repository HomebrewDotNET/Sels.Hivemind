using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Factory that can be used to create new <see cref="IColony"/>(s)
    /// </summary>
    public interface IColonyFactory
    {
        /// <summary>
        /// Creates a new <see cref="IColony"/>.
        /// </summary>
        /// <param name="builder">Delegate used to configure the new <see cref="IColony"/></param>
        /// <returns>A new <see cref="IColony"/> configured by <paramref name="builder"/></returns>
        IColony Create(Action<IColonyBuilder> builder);
    }
}
