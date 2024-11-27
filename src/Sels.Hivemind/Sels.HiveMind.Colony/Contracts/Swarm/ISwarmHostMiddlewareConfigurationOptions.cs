using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    ///  Exposes more options for configuring middleware that is defined on a swarm.
    /// </summary>
    public interface ISwarmHostMiddlewareConfigurationOptions
    {
        /// <inheritdoc cref="SwarmMiddlewareInheritanceBehaviour"/>
        public SwarmMiddlewareInheritanceBehaviour InheritanceBehaviour { get; }
        /// <summary>
        /// Limits the depth of inheritance for parent swarms. If set to null no limit is enforced.
        /// For example if you only want the middleare to be used when fetching jobs from the direct parent of the swarm where the middleware is defined on set this to 1.
        /// </summary>
        public byte? MaxInheritanceDepth { get; }
        /// <inheritdoc cref="IMiddleware.Priority"/>
        public byte? Priority { get; }
    }
}
