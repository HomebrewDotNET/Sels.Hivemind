using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sels.HiveMind.Colony.Swarm;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Exposes more options for configuring middleware that is defined on a swarm.
    /// </summary>
    public class SwarmHostMiddlewareConfigurationOptions : ISwarmHostMiddlewareConfigurationOptions
    {
        // Properties
        /// <inheritdoc cref="SwarmMiddlewareInheritanceBehaviour"/>
        public SwarmMiddlewareInheritanceBehaviour InheritanceBehaviour { get; set; } = SwarmMiddlewareInheritanceBehaviour.Inherit;
        /// <inheritdoc/>
        public byte? MaxInheritanceDepth { get; set; } = 0;
        /// <inheritdoc />
        public byte? Priority { get; set; }
    }
}
